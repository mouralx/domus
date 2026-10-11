using Npgsql;
using NpgsqlTypes;

namespace Automations.Core;

public sealed class Database : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; }
    public Database(string connectionString) => Source = NpgsqlDataSource.Create(connectionString);
    public static string ConnectionString() => Environment.GetEnvironmentVariable("DATABASE_URL") ?? new NpgsqlConnectionStringBuilder
    {
        Host = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "localhost",
        Port = int.Parse(Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432"),
        Database = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "automations",
        Username = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "automations",
        Password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "local-postgres-change-me"
    }.ConnectionString;
    public async Task Initialize(CancellationToken ct = default)
    {
        await using var connection = await Source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(4310320);
            CREATE TABLE IF NOT EXISTS accounts(id uuid PRIMARY KEY, username text NOT NULL, normalized_username text NOT NULL UNIQUE, password_hash text NOT NULL);
            CREATE TABLE IF NOT EXISTS platform_github_settings(singleton boolean PRIMARY KEY CHECK(singleton), repository text, encrypted_token text);
            CREATE TABLE IF NOT EXISTS github_preferences(user_id uuid PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE, repository text NOT NULL);
            CREATE TABLE IF NOT EXISTS github_credentials(user_id uuid PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE, encrypted_token text NOT NULL);
            CREATE TABLE IF NOT EXISTS sessions(token_hash text PRIMARY KEY, user_id uuid NOT NULL REFERENCES accounts(id), expires_at timestamptz NOT NULL);
            CREATE TABLE IF NOT EXISTS packages(user_id uuid NOT NULL REFERENCES accounts(id), key text NOT NULL, manifest jsonb NOT NULL, digest text NOT NULL, PRIMARY KEY(user_id,key));
            CREATE TABLE IF NOT EXISTS uploads(id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES accounts(id), name text NOT NULL, filename text NOT NULL);
            CREATE TABLE IF NOT EXISTS executions(id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES accounts(id), status text NOT NULL, record jsonb NOT NULL, cancel_requested boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS execution_outbox(run_id uuid PRIMARY KEY REFERENCES executions(id), published_at timestamptz);
            CREATE TABLE IF NOT EXISTS installations(id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES accounts(id), automation_id text NOT NULL, status text NOT NULL, recipe jsonb NOT NULL, error text, created_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS installation_outbox(job_id uuid PRIMARY KEY REFERENCES installations(id), published_at timestamptz);
            CREATE UNIQUE INDEX IF NOT EXISTS installations_active ON installations(user_id,automation_id) WHERE status IN ('queued','installing');
            CREATE INDEX IF NOT EXISTS executions_user_created ON executions(user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS sessions_expiry ON sessions(expires_at);
            """, connection, transaction);
        await cmd.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task EnsureWorkspace(Guid workspace)
    {
        await using var command = Source.CreateCommand("INSERT INTO accounts(id,username,normalized_username,password_hash) VALUES ($1,$2,$2,'domus-disabled') ON CONFLICT (id) DO NOTHING");
        command.Parameters.AddWithValue(workspace);
        command.Parameters.AddWithValue("workspace:" + workspace);
        await command.ExecuteNonQueryAsync();
    }
    public async Task<bool> Healthy(CancellationToken ct) { await using var command = Source.CreateCommand("SELECT 1"); return (int)(await command.ExecuteScalarAsync(ct))! == 1; }
    public async Task<Account?> FindAccount(string normalized)
    {
        await using var cmd = Source.CreateCommand("SELECT id,username,password_hash FROM accounts WHERE normalized_username=$1"); cmd.Parameters.AddWithValue(normalized);
        await using var reader = await cmd.ExecuteReaderAsync(); return await reader.ReadAsync() ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)) : null;
    }
    public async Task<Account> Register(string username, string passwordHash)
    {
        var user = new Account(Guid.NewGuid(), username.Trim(), passwordHash);
        await using var cmd = Source.CreateCommand("INSERT INTO accounts VALUES($1,$2,$3,$4)");
        cmd.Parameters.AddWithValue(user.Id); cmd.Parameters.AddWithValue(user.Username); cmd.Parameters.AddWithValue(user.Username.ToLowerInvariant()); cmd.Parameters.AddWithValue(passwordHash);
        try { await cmd.ExecuteNonQueryAsync(); } catch (PostgresException e) when (e.SqlState == "23505") { throw new AppError(409, "Este utilizador já existe."); }
        return user;
    }
    public async Task Session(string tokenHash, Guid user)
    {
        await using var cmd = Source.CreateCommand("INSERT INTO sessions VALUES($1,$2,now()+interval '12 hours')"); cmd.Parameters.AddWithValue(tokenHash); cmd.Parameters.AddWithValue(user); await cmd.ExecuteNonQueryAsync();
    }
    public async Task<Guid?> Authenticate(string tokenHash)
    {
        await using var cmd = Source.CreateCommand("SELECT user_id FROM sessions WHERE token_hash=$1 AND expires_at>now()"); cmd.Parameters.AddWithValue(tokenHash); return await cmd.ExecuteScalarAsync() is Guid id ? id : null;
    }
    public async Task Logout(string tokenHash) { await using var cmd = Source.CreateCommand("DELETE FROM sessions WHERE token_hash=$1"); cmd.Parameters.AddWithValue(tokenHash); await cmd.ExecuteNonQueryAsync(); }
    public async Task<List<Package>> Packages(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT key,manifest::text,digest FROM packages WHERE user_id=$1 ORDER BY key"); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); var list = new List<Package>();
        while (await reader.ReadAsync()) list.Add(new(reader.GetString(0), System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(1))!.AsObject(), reader.GetString(2))); return list;
    }
    public async Task<Package?> Package(Guid user, string key)
    {
        await using var cmd = Source.CreateCommand("SELECT manifest::text,digest FROM packages WHERE user_id=$1 AND key=$2"); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(key);
        await using var reader = await cmd.ExecuteReaderAsync(); return await reader.ReadAsync() ? new(key, System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(0))!.AsObject(), reader.GetString(1)) : null;
    }
    public async Task SavePackage(Guid user, Package pkg)
    {
        await using var cmd = Source.CreateCommand("INSERT INTO packages VALUES($1,$2,$3,$4) ON CONFLICT(user_id,key) DO UPDATE SET manifest=excluded.manifest");
        cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(pkg.Key); cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, pkg.Manifest.ToJsonString()); cmd.Parameters.AddWithValue(pkg.Digest); await cmd.ExecuteNonQueryAsync();
    }
    public async Task<Package> RemovePackage(Guid user, string key)
    {
        await using var conn = await Source.OpenConnectionAsync(); await using var tx = await conn.BeginTransactionAsync();
        await using var locked = new NpgsqlCommand("SELECT manifest::text,digest FROM packages WHERE user_id=$1 AND key=$2 FOR UPDATE", conn, tx);
        locked.Parameters.AddWithValue(user); locked.Parameters.AddWithValue(key);
        Package package;
        await using (var reader = await locked.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new AppError(404, "Automação não encontrada.");
            package = new(key, System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(0))!.AsObject(), reader.GetString(1));
        }
        await using var active = new NpgsqlCommand("SELECT 1 FROM executions WHERE user_id=$1 AND record->>'packageKey'=$2 AND status IN ('queued','starting','running','awaiting-user') LIMIT 1", conn, tx);
        active.Parameters.AddWithValue(user); active.Parameters.AddWithValue(key);
        if (await active.ExecuteScalarAsync() is not null) throw new AppError(409, "Termine ou cancele as execuções desta automação antes de a remover.");
        await using var installing = new NpgsqlCommand("SELECT 1 FROM installations WHERE user_id=$1 AND automation_id=$2 AND status IN ('queued','installing') LIMIT 1",conn,tx); installing.Parameters.AddWithValue(user); installing.Parameters.AddWithValue(package.Manifest["id"]!.GetValue<string>());
        if(await installing.ExecuteScalarAsync() is not null) throw new AppError(409,"Aguarde a instalação ou atualização desta automação antes de a remover.");
        await using var delete = new NpgsqlCommand("DELETE FROM packages WHERE user_id=$1 AND key=$2", conn, tx); delete.Parameters.AddWithValue(user); delete.Parameters.AddWithValue(key); await delete.ExecuteNonQueryAsync(); await tx.CommitAsync(); return package;
    }
    public async Task SaveUpload(Guid user, Upload upload)
    {
        await using var cmd = Source.CreateCommand("INSERT INTO uploads VALUES($1,$2,$3,$4)"); cmd.Parameters.AddWithValue(upload.Id); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(upload.Name); cmd.Parameters.AddWithValue(upload.Filename); await cmd.ExecuteNonQueryAsync();
    }
    public async Task<Upload?> Upload(Guid user, Guid id)
    {
        await using var cmd = Source.CreateCommand("SELECT name,filename FROM uploads WHERE id=$1 AND user_id=$2"); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); return await reader.ReadAsync() ? new(id, reader.GetString(0), reader.GetString(1)) : null;
    }
    public async Task Enqueue(Guid user, RunRecord run)
    {
        await using var conn = await Source.OpenConnectionAsync(); await using var tx = await conn.BeginTransactionAsync();
        await using var packageLock = new NpgsqlCommand("SELECT 1 FROM packages WHERE user_id=$1 AND key=$2 FOR KEY SHARE", conn, tx);
        packageLock.Parameters.AddWithValue(user); packageLock.Parameters.AddWithValue(run.PackageKey);
        if (await packageLock.ExecuteScalarAsync() is null) throw new AppError(404, "Pacote não encontrado.");
        await using var cmd = new NpgsqlCommand("INSERT INTO executions(id,user_id,status,record) VALUES($1,$2,'queued',$3)", conn, tx);
        cmd.Parameters.AddWithValue(run.Id); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, Json.Write(run)); await cmd.ExecuteNonQueryAsync();
        await using var outbox = new NpgsqlCommand("INSERT INTO execution_outbox(run_id) VALUES($1)", conn, tx); outbox.Parameters.AddWithValue(run.Id); await outbox.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }
    public async Task<Execution?> Execution(Guid id, Guid? user = null)
    {
        await using var cmd = Source.CreateCommand("SELECT user_id,status,record::text,cancel_requested FROM executions WHERE id=$1" + (user is null ? "" : " AND user_id=$2")); cmd.Parameters.AddWithValue(id); if (user is not null) cmd.Parameters.AddWithValue(user.Value);
        await using var reader = await cmd.ExecuteReaderAsync(); return await reader.ReadAsync() ? new(id, reader.GetGuid(0), reader.GetString(1), Json.Read<RunRecord>(reader.GetString(2)), reader.GetBoolean(3)) : null;
    }
    public async Task<InstallationJob> EnqueueInstallation(Guid user, InstallationRecipe recipe)
    {
        var id = Guid.NewGuid();
        await using var connection = await Source.OpenConnectionAsync(); await using var tx = await connection.BeginTransactionAsync();
        await using var save = new NpgsqlCommand("INSERT INTO installations(id,user_id,automation_id,status,recipe) VALUES($1,$2,$3,'queued',$4)", connection, tx); save.Parameters.AddWithValue(id); save.Parameters.AddWithValue(user); save.Parameters.AddWithValue(recipe.AutomationId); save.Parameters.AddWithValue(NpgsqlDbType.Jsonb, Json.Write(recipe));
        try { await save.ExecuteNonQueryAsync(); } catch (PostgresException error) when (error.SqlState == "23505") { throw new AppError(409, "Já existe uma instalação desta automação na fila ou em curso."); }
        await using var outbox = new NpgsqlCommand("INSERT INTO installation_outbox(job_id) VALUES($1)", connection, tx); outbox.Parameters.AddWithValue(id); await outbox.ExecuteNonQueryAsync(); await tx.CommitAsync();
        return (await Installation(id, user))!;
    }
    public async Task<InstallationJob?> Installation(Guid id, Guid? user = null)
    {
        await using var cmd = Source.CreateCommand("SELECT user_id,status,recipe::text,error,created_at FROM installations WHERE id=$1" + (user is null ? "" : " AND user_id=$2")); cmd.Parameters.AddWithValue(id); if (user is not null) cmd.Parameters.AddWithValue(user.Value);
        await using var reader = await cmd.ExecuteReaderAsync(); return await reader.ReadAsync() ? new(id,reader.GetGuid(0),reader.GetString(1),Json.Read<InstallationRecipe>(reader.GetString(2)),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetFieldValue<DateTimeOffset>(4)) : null;
    }
    public async Task<List<InstallationJob>> Installations(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT DISTINCT ON (automation_id) id,status,recipe::text,error,created_at FROM installations WHERE user_id=$1 ORDER BY automation_id,created_at DESC"); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); var jobs = new List<InstallationJob>(); while(await reader.ReadAsync()) jobs.Add(new(reader.GetGuid(0),user,reader.GetString(1),Json.Read<InstallationRecipe>(reader.GetString(2)),reader.IsDBNull(3)?null:reader.GetString(3),reader.GetFieldValue<DateTimeOffset>(4))); return jobs;
    }
    public async Task<bool> ClaimInstallation(Guid id)
    {
        await using var cmd = Source.CreateCommand("UPDATE installations SET status='installing' WHERE id=$1 AND status='queued'"); cmd.Parameters.AddWithValue(id); return await cmd.ExecuteNonQueryAsync() == 1;
    }
    public async Task FinishInstallation(Guid id, string status, string? error = null)
    {
        await using var cmd = Source.CreateCommand("UPDATE installations SET status=$2,error=$3 WHERE id=$1"); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(status); cmd.Parameters.AddWithValue(NpgsqlDbType.Text,(object?)error ?? DBNull.Value); await cmd.ExecuteNonQueryAsync();
    }
    public async Task RecoverInstallations()
    {
        await using var cmd = Source.CreateCommand("UPDATE installations SET status='failed',error='Instalação interrompida. Tente novamente.' WHERE status='installing'"); await cmd.ExecuteNonQueryAsync();
    }
    public async Task<RunMetrics> Metrics(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT count(*) FILTER (WHERE status IN ('queued','starting','running','awaiting-user')),count(*) FILTER (WHERE status IN ('completed','failed')) FROM executions WHERE user_id=$1"); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); await reader.ReadAsync(); return new(reader.GetInt64(0), reader.GetInt64(1));
    }
    public async Task<List<RunRecord>> Runs(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT record::text FROM executions WHERE user_id=$1 ORDER BY created_at DESC LIMIT 100"); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); var runs = new List<RunRecord>(); while (await reader.ReadAsync()) runs.Add(Json.Read<RunRecord>(reader.GetString(0))); return runs;
    }
    public async Task<object> McpCounts(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT (SELECT count(*) FROM packages WHERE user_id=$1),(SELECT count(*) FROM executions WHERE user_id=$1),(SELECT count(*) FROM installations WHERE user_id=$1)"); cmd.Parameters.AddWithValue(user);
        await using var reader = await cmd.ExecuteReaderAsync(); await reader.ReadAsync();
        return new { packages = reader.GetInt64(0), runs = reader.GetInt64(1), installations = reader.GetInt64(2) };
    }
    public async Task<object> McpList(Guid user, string kind, int offset, int limit)
    {
        var sql = kind switch
        {
            "packages" => "SELECT jsonb_build_object('id',key,'name',manifest->>'name','version',manifest->>'version')::text FROM packages WHERE user_id=$1 ORDER BY key OFFSET $2 LIMIT $3",
            "runs" => "SELECT jsonb_build_object('id',id,'name',record->>'name','status',status,'packageKey',record->>'packageKey','startedAt',record->>'startedAt')::text FROM executions WHERE user_id=$1 ORDER BY created_at DESC,id OFFSET $2 LIMIT $3",
            "installations" => "SELECT jsonb_build_object('id',id,'status',status,'automationId',automation_id,'createdAt',created_at)::text FROM installations WHERE user_id=$1 ORDER BY created_at DESC,id OFFSET $2 LIMIT $3",
            _ => throw new AppError(400, "Coleção inválida.")
        };
        await using var cmd = Source.CreateCommand(sql); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(offset); cmd.Parameters.AddWithValue(limit + 1);
        await using var reader = await cmd.ExecuteReaderAsync(); var rows = new List<System.Text.Json.Nodes.JsonNode>();
        while (await reader.ReadAsync()) rows.Add(System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(0))!);
        return new { items = rows.Take(limit).ToArray(), nextOffset = rows.Count > limit ? (int?)(offset + limit) : null, hasMore = rows.Count > limit };
    }
    public async Task<bool> Claim(Guid id)
    {
        await using var cmd = Source.CreateCommand("UPDATE executions SET status='starting',record=jsonb_set(record,'{status}','\"starting\"') WHERE id=$1 AND status='queued'"); cmd.Parameters.AddWithValue(id); return await cmd.ExecuteNonQueryAsync() == 1;
    }
    public async Task SaveRun(RunRecord run)
    {
        await using var cmd = Source.CreateCommand("UPDATE executions SET status=$2,record=$3 WHERE id=$1"); cmd.Parameters.AddWithValue(run.Id); cmd.Parameters.AddWithValue(run.Status); cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, Json.Write(run)); await cmd.ExecuteNonQueryAsync();
    }
    public async Task Cancel(Guid id, Guid user)
    {
        await using var cmd = Source.CreateCommand("UPDATE executions SET cancel_requested=true WHERE id=$1 AND user_id=$2 AND status IN ('queued','starting','running','awaiting-user')"); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(user); if (await cmd.ExecuteNonQueryAsync() != 1) throw new AppError(409, "Esta execução já terminou.");
    }
    public async Task<List<(Guid Id, Guid User)>> RecoverInterrupted()
    {
        await using var cmd = Source.CreateCommand("UPDATE executions SET status='failed',record=record || jsonb_build_object('status','failed','endedAt',now(),'message','Execução interrompida. Inicie uma nova execução.') WHERE status IN ('starting','running','awaiting-user') RETURNING id,user_id");
        await using var reader = await cmd.ExecuteReaderAsync(); var rows = new List<(Guid, Guid)>(); while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetGuid(1))); return rows;
    }
    public async Task<string?> GitHubRepository(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT repository FROM github_preferences WHERE user_id=$1"); cmd.Parameters.AddWithValue(user); return await cmd.ExecuteScalarAsync() as string;
    }
    public async Task SaveGitHubSettings(Guid user, string? repository, string? encrypted)
    {
        await using var connection = await Source.OpenConnectionAsync(); await using var tx = await connection.BeginTransactionAsync();
        if (repository is not null)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO github_preferences VALUES($1,$2) ON CONFLICT(user_id) DO UPDATE SET repository=excluded.repository", connection, tx); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(repository); await cmd.ExecuteNonQueryAsync();
        }
        if (encrypted is not null)
        {
            await using var cmd = new NpgsqlCommand("INSERT INTO github_credentials VALUES($1,$2) ON CONFLICT(user_id) DO UPDATE SET encrypted_token=excluded.encrypted_token", connection, tx); cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(encrypted); await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
    public async Task ActivateCatalogPackage(Guid user, Package pkg)
    {
        await using var connection = await Source.OpenConnectionAsync(); await using var tx = await connection.BeginTransactionAsync();
        await using var locked = new NpgsqlCommand("SELECT key FROM packages WHERE user_id=$1 AND manifest->>'id'=$2 FOR UPDATE", connection, tx); locked.Parameters.AddWithValue(user); locked.Parameters.AddWithValue(pkg.Manifest["id"]!.GetValue<string>());
        var keys = new List<string>(); await using (var reader = await locked.ExecuteReaderAsync()) while (await reader.ReadAsync()) keys.Add(reader.GetString(0));
        await using var active = new NpgsqlCommand("SELECT 1 FROM executions WHERE user_id=$1 AND record->>'packageKey'=ANY($2) AND status IN ('queued','starting','running','awaiting-user') LIMIT 1", connection, tx); active.Parameters.AddWithValue(user); active.Parameters.AddWithValue(keys.ToArray());
        if (await active.ExecuteScalarAsync() is not null) throw new AppError(409, "Termine ou cancele as execuções desta automação antes de a atualizar.");
        await using var delete = new NpgsqlCommand("DELETE FROM packages WHERE user_id=$1 AND manifest->>'id'=$2", connection, tx); delete.Parameters.AddWithValue(user); delete.Parameters.AddWithValue(pkg.Manifest["id"]!.GetValue<string>()); await delete.ExecuteNonQueryAsync();
        await using var save = new NpgsqlCommand("INSERT INTO packages VALUES($1,$2,$3,$4)", connection, tx); save.Parameters.AddWithValue(user); save.Parameters.AddWithValue(pkg.Key); save.Parameters.AddWithValue(NpgsqlDbType.Jsonb, pkg.Manifest.ToJsonString()); save.Parameters.AddWithValue(pkg.Digest); await save.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }
    public async Task<string?> GitHubToken(Guid user)
    {
        await using var cmd = Source.CreateCommand("SELECT encrypted_token FROM github_credentials WHERE user_id=$1"); cmd.Parameters.AddWithValue(user); return await cmd.ExecuteScalarAsync() as string;
    }
    public async Task SaveGitHubToken(Guid user, string encrypted)
    {
        await using var cmd = Source.CreateCommand("INSERT INTO github_credentials VALUES($1,$2) ON CONFLICT(user_id) DO UPDATE SET encrypted_token=excluded.encrypted_token");
        cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(encrypted); await cmd.ExecuteNonQueryAsync();
    }
    public async Task RemoveGitHubToken(Guid user)
    {
        await using var cmd = Source.CreateCommand("DELETE FROM github_credentials WHERE user_id=$1"); cmd.Parameters.AddWithValue(user); await cmd.ExecuteNonQueryAsync();
    }
    public async ValueTask DisposeAsync() => await Source.DisposeAsync();
}
