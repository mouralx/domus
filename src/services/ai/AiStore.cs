using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Domus.Ai;

// A single service instance owns this volume. Atomic writes persist settings and
// usage reservations and encrypted OAuth connections across restarts.
public sealed class AiStore : IDisposable
{
    public void Dispose() => sql?.Dispose();
    private readonly SqlAiStore? sql;
    private readonly string directory;
    public Task Initialize() => sql?.Initialize(directory) ?? Task.CompletedTask;
    private readonly SemaphoreSlim gate = new(1);
    private readonly IDataProtector protector;
    private readonly string path;
    private StoreState state;
    public AiStore(IDataProtectionProvider protection, IConfiguration configuration)
    {
        protector = protection.CreateProtector("Domus.AI.Credentials.v1");
        directory = configuration["AI_DATA_DIR"] ?? ".local/ai";
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        path = Path.Combine(directory, "settings.json");
        if (configuration.GetConnectionString("Postgres") is { Length: > 0 } connection) sql = new SqlAiStore(connection, protection);
        state = sql is null && File.Exists(path) ? JsonSerializer.Deserialize<StoreState>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("AI settings are invalid.") : new();
    }
    private async Task<T> Locked<T>(Func<Task<T>> action)
    {
        await gate.WaitAsync();
        try { return await action(); } finally { gate.Release(); }
    }
    private async Task Save()
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, path, true);
    }
    public Task<string> HostId() => sql?.HostId() ?? Locked(async () => { await Save(); return state.HostId; });
    public Task<(ChatGptConnection? Connection, Guid Revision)> Connection(Guid actor) => sql?.Connection(actor) ?? Locked(() => Task.FromResult((
        state.Connections.TryGetValue(actor, out var encrypted) ? JsonSerializer.Deserialize<ChatGptConnection>(protector.CreateProtector(actor.ToString()).Unprotect(encrypted)) : null,
        state.Revisions.GetValueOrDefault(actor))));
    public Task<bool> TransferUsed(string key) => sql?.TransferUsed(key) ?? Locked(() => Task.FromResult(state.UsedTransfers.Contains(key)));
    public Task SaveConnection(Guid actor, ChatGptConnection? connection, Guid expected, string? transferKey = null) => sql?.SaveConnection(actor, connection, expected, transferKey) ?? Locked(async () =>
    {
        if (state.Revisions.GetValueOrDefault(actor) != expected) throw new AiError(409, "A ligação mudou. Tente novamente.");
        if (transferKey is not null && state.UsedTransfers.Contains(transferKey)) throw new AiError(409, "Esta transferência já foi utilizada.");
        var old = state;
        state = new() { UsedTransfers = new(state.UsedTransfers), HostId = state.HostId, Connections = new(state.Connections), Revisions = new(state.Revisions), Workspaces = new(state.Workspaces), Usage = [.. state.Usage] };
        if (transferKey is not null) state.UsedTransfers.Add(transferKey);
        state.Revisions[actor] = Guid.NewGuid();
        if (connection is null) state.Connections.Remove(actor);
        else state.Connections[actor] = protector.CreateProtector(actor.ToString()).Protect(JsonSerializer.Serialize(connection));
        try { await Save(); } catch { state = old; throw; }
        return true;
    });
    public Task<WorkspaceConfig> Workspace(Guid id) => sql?.Workspace(id) ?? Locked(() => Task.FromResult(state.Workspaces.GetValueOrDefault(id) ?? new(false, "openai")));
    public Task SaveWorkspace(Guid id, WorkspaceConfig input) => sql?.SaveWorkspace(id, input) ?? Locked(async () =>
    {
        if (input.Provider != "openai") throw new AiError(400, "Escolha a ligação ChatGPT.");
        var old = state;
        state = new() { UsedTransfers = new(state.UsedTransfers), HostId = state.HostId, Connections = new(state.Connections), Revisions = new(state.Revisions), Workspaces = new(state.Workspaces), Usage = [.. state.Usage] };
        state.Workspaces[id] = input;
        try { await Save(); } catch { state = old; throw; }
        return true;
    });
    public Task<UsageRecord> Reserve(Identity actor, string model, string action) => sql?.Reserve(actor, model, action) ?? Locked(async () =>
    {
        var config = state.Workspaces.GetValueOrDefault(actor.WorkspaceId);
        if (config?.Enabled != true || config.Provider != "openai") throw new AiError(409, "A IA está desativada neste workspace ou a configuração mudou.");
        if (!state.Connections.ContainsKey(actor.UserId)) throw new AiError(409, "Ligue a sua conta ChatGPT nas definições de IA.");
        var now = DateTimeOffset.UtcNow;
        var current = state.Usage.Where(u => u.StartedAt.Year == now.Year && u.StartedAt.Month == now.Month).ToList();
        if (current.Count(u => u.Workspace == actor.WorkspaceId && u.Actor == actor.UserId && u.StartedAt > now.AddMinutes(-1)) >= 10) throw new AiError(429, "Demasiados pedidos de IA. Aguarde um minuto.");
        if (current.Count(u => u.Workspace == actor.WorkspaceId && u.Status == "pending" && u.StartedAt > now.AddMinutes(-3)) >= 3) throw new AiError(429, "Já existem três pedidos de IA em curso neste workspace.");
        var record = new UsageRecord(Guid.NewGuid(), actor.WorkspaceId, actor.UserId, "openai", model, action, now, "pending");
        var old = state.Usage;
        state.Usage = [.. current, record];
        try { await Save(); } catch { state.Usage = old; throw; }
        return record;
    });
    public Task Finish(Guid id, string result, long input = 0, long output = 0) => sql?.Finish(id, result, input, output) ?? Locked(async () =>
    {
        var index = state.Usage.FindIndex(u => u.Id == id);
        if (index >= 0) { state.Usage[index] = state.Usage[index] with { Status = result, InputTokens = input, OutputTokens = output }; await Save(); }
        return true;
    });
    public Task<object> Usage(Guid workspace) => sql?.Usage(workspace) ?? Locked(() =>
    {
        var now = DateTimeOffset.UtcNow;
        var rows = state.Usage.Where(u => u.Workspace == workspace && u.StartedAt.Year == now.Year && u.StartedAt.Month == now.Month).ToArray();
        return Task.FromResult<object>(new { month = now.ToString("yyyy-MM"), requests = rows.Length, inputTokens = rows.Sum(u => u.InputTokens), outputTokens = rows.Sum(u => u.OutputTokens) });
    });
}
