using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using System.Text.Json;

namespace Domus.Ai;
// AI owns these tables and credentials. No access to boards or membership databases.
public sealed class SqlAiStore : IDisposable
{
    private readonly NpgsqlDataSource data;
    private readonly IDataProtector protector;
    public SqlAiStore(string connection, IDataProtectionProvider protection)
    {
        data = NpgsqlDataSource.Create(connection);
        protector = protection.CreateProtector("Domus.AI.Credentials.v1");
    }
    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            if (value is null) command.Parameters.AddWithValue(name, NpgsqlTypes.NpgsqlDbType.Text, DBNull.Value);
            else command.Parameters.AddWithValue(name, value);
        }
        return command;
    }
    public async Task Initialize(string directory)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = Command(connection, "SELECT pg_advisory_xact_lock(4330)")) await command.ExecuteNonQueryAsync();
        await using (var command = Command(connection, """
            CREATE TABLE IF NOT EXISTS ai_metadata (key text PRIMARY KEY, value text NOT NULL);
            CREATE TABLE IF NOT EXISTS ai_connections (actor uuid PRIMARY KEY, encrypted text, revision uuid NOT NULL);
            CREATE TABLE IF NOT EXISTS ai_workspaces (workspace uuid PRIMARY KEY, enabled boolean NOT NULL, provider text NOT NULL CHECK(provider='openai'));
            CREATE TABLE IF NOT EXISTS ai_usage (id uuid PRIMARY KEY, workspace uuid NOT NULL, actor uuid NOT NULL, provider text NOT NULL, model text NOT NULL, action text NOT NULL, started_at timestamptz NOT NULL, status text NOT NULL, input_tokens bigint NOT NULL DEFAULT 0, output_tokens bigint NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ai_usage_workspace_started ON ai_usage(workspace, started_at);
            """)) await command.ExecuteNonQueryAsync();
        // Import once, atomically, preserving existing encrypted credentials, revisions and host identity.
        await using var imported = Command(connection, "SELECT value FROM ai_metadata WHERE key='legacy_imported'");
        if (await imported.ExecuteScalarAsync() is null)
        {
            var path = Path.Combine(directory, "settings.json");
            var legacy = File.Exists(path) ? JsonSerializer.Deserialize<StoreState>(await File.ReadAllTextAsync(path)) ?? throw new InvalidOperationException("Invalid legacy AI data.") : new();
            await using (var host = Command(connection, "INSERT INTO ai_metadata(key,value) VALUES('host_id',@value) ON CONFLICT DO NOTHING", ("value", legacy.HostId))) await host.ExecuteNonQueryAsync();
            foreach (var actor in legacy.Connections.Keys.Union(legacy.Revisions.Keys))
            {
                await using var command = Command(connection, "INSERT INTO ai_connections(actor,encrypted,revision) VALUES(@actor,@encrypted,@revision) ON CONFLICT DO NOTHING", ("actor", actor), ("encrypted", legacy.Connections.GetValueOrDefault(actor)), ("revision", legacy.Revisions.GetValueOrDefault(actor)));
                await command.ExecuteNonQueryAsync();
            }
            foreach (var (workspace, config) in legacy.Workspaces)
            {
                await using var command = Command(connection, "INSERT INTO ai_workspaces VALUES(@workspace,@enabled,@provider) ON CONFLICT DO NOTHING", ("workspace", workspace), ("enabled", config.Enabled), ("provider", config.Provider));
                await command.ExecuteNonQueryAsync();
            }
            foreach (var row in legacy.Usage) await InsertUsage(connection, row);
            await using var marker = Command(connection, "INSERT INTO ai_metadata VALUES('legacy_imported','true')");
            await marker.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
    public async Task<string> HostId()
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var command = Command(connection, "SELECT value FROM ai_metadata WHERE key='host_id'");
        return (string)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("AI database not initialized."));
    }
    public async Task<(ChatGptConnection? Connection, Guid Revision)> Connection(Guid actor)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var command = Command(connection, "SELECT encrypted,revision FROM ai_connections WHERE actor=@actor", ("actor", actor));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (null, Guid.Empty);
        return (reader.IsDBNull(0) ? null : JsonSerializer.Deserialize<ChatGptConnection>(protector.CreateProtector(actor.ToString()).Unprotect(reader.GetString(0))), reader.GetGuid(1));
    }
    public async Task SaveConnection(Guid actor, ChatGptConnection? credential, Guid expected)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = Command(connection, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "actor:" + actor))) await lockCommand.ExecuteNonQueryAsync();
        await using var read = Command(connection, "SELECT revision FROM ai_connections WHERE actor=@actor", ("actor", actor));
        var revision = (Guid?)await read.ExecuteScalarAsync() ?? Guid.Empty;
        if (revision != expected) throw new AiError(409, "A ligação mudou. Tente novamente.");
        var encrypted = credential is null ? null : protector.CreateProtector(actor.ToString()).Protect(JsonSerializer.Serialize(credential));
        await using var save = Command(connection, "INSERT INTO ai_connections VALUES(@actor,@encrypted,@revision) ON CONFLICT(actor) DO UPDATE SET encrypted=EXCLUDED.encrypted, revision=EXCLUDED.revision", ("actor", actor), ("encrypted", encrypted), ("revision", Guid.NewGuid()));
        await save.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
    public async Task<WorkspaceConfig> Workspace(Guid workspace)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var command = Command(connection, "SELECT enabled,provider FROM ai_workspaces WHERE workspace=@workspace", ("workspace", workspace));
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new(reader.GetBoolean(0), reader.GetString(1)) : new(false, "openai");
    }
    public async Task SaveWorkspace(Guid workspace, WorkspaceConfig config)
    {
        if (config.Provider != "openai") throw new AiError(400, "Escolha a ligação ChatGPT.");
        await using var connection = await data.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await LockWorkspace(connection, workspace);
        await using var command = Command(connection, "INSERT INTO ai_workspaces VALUES(@workspace,@enabled,@provider) ON CONFLICT(workspace) DO UPDATE SET enabled=EXCLUDED.enabled,provider=EXCLUDED.provider", ("workspace", workspace), ("enabled", config.Enabled), ("provider", config.Provider));
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
    private static async Task LockWorkspace(NpgsqlConnection connection, Guid workspace)
    {
        await using var command = Command(connection, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "workspace:" + workspace));
        await command.ExecuteNonQueryAsync();
    }
    public async Task<UsageRecord> Reserve(Identity actor, string model, string action)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await LockWorkspace(connection, actor.WorkspaceId);
        await using var enabled = Command(connection, "SELECT enabled FROM ai_workspaces WHERE workspace=@workspace", ("workspace", actor.WorkspaceId));
        if (await enabled.ExecuteScalarAsync() is not true) throw new AiError(409, "A IA está desativada neste workspace ou a configuração mudou.");
        await using var linked = Command(connection, "SELECT EXISTS(SELECT 1 FROM ai_connections WHERE actor=@actor AND encrypted IS NOT NULL)", ("actor", actor.UserId));
        if (await linked.ExecuteScalarAsync() is not true) throw new AiError(409, "Ligue a sua conta ChatGPT nas definições de IA.");
        await using var limits = Command(connection, "SELECT count(*) FILTER(WHERE actor=@actor AND started_at>now()-interval '1 minute'), count(*) FILTER(WHERE status='pending' AND started_at>now()-interval '3 minutes') FROM ai_usage WHERE workspace=@workspace AND started_at>now()-interval '3 minutes'", ("actor", actor.UserId), ("workspace", actor.WorkspaceId));
        await using (var reader = await limits.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            if (reader.GetInt64(0) >= 10) throw new AiError(429, "Demasiados pedidos de IA. Aguarde um minuto.");
            if (reader.GetInt64(1) >= 3) throw new AiError(429, "Já existem três pedidos de IA em curso neste workspace.");
        }
        var row = new UsageRecord(Guid.NewGuid(), actor.WorkspaceId, actor.UserId, "openai", model, action, DateTimeOffset.UtcNow, "pending");
        await InsertUsage(connection, row);
        await transaction.CommitAsync();
        return row;
    }
    private static async Task InsertUsage(NpgsqlConnection connection, UsageRecord row)
    {
        await using var command = Command(connection, "INSERT INTO ai_usage VALUES(@id,@workspace,@actor,@provider,@model,@action,@started,@status,@input,@output) ON CONFLICT DO NOTHING", ("id", row.Id), ("workspace", row.Workspace), ("actor", row.Actor), ("provider", row.Provider), ("model", row.Model), ("action", row.Action), ("started", row.StartedAt), ("status", row.Status), ("input", row.InputTokens), ("output", row.OutputTokens));
        await command.ExecuteNonQueryAsync();
    }
    public async Task Finish(Guid id, string status, long input, long output)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var command = Command(connection, "UPDATE ai_usage SET status=@status,input_tokens=@input,output_tokens=@output WHERE id=@id", ("id", id), ("status", status), ("input", input), ("output", output));
        await command.ExecuteNonQueryAsync();
    }
    public async Task<object> Usage(Guid workspace)
    {
        await using var connection = await data.OpenConnectionAsync();
        await using var command = Command(connection, "SELECT count(*),coalesce(sum(input_tokens),0)::bigint,coalesce(sum(output_tokens),0)::bigint FROM ai_usage WHERE workspace=@workspace AND started_at>=date_trunc('month',now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'", ("workspace", workspace));
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        return new { month = DateTimeOffset.UtcNow.ToString("yyyy-MM"), requests = reader.GetInt64(0), inputTokens = reader.GetInt64(1), outputTokens = reader.GetInt64(2) };
    }
    public void Dispose() => data.Dispose();
}
