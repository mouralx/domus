namespace Domus.Ai;

public sealed class AiError(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed record Identity(Guid UserId, Guid WorkspaceId, bool CanManage, bool CanManagePlatform);
public sealed record WorkspaceConfig(bool Enabled, string Provider);
public sealed record SuggestionInput(string CardId, string Action, string Locale);
public sealed record Suggestion(string Text, string[] Items);
public sealed record Generation(Suggestion Suggestion, long InputTokens, long OutputTokens);
public sealed record ChatGptConnection(string ClientId, string Subject, string Email, string AccessToken, string RefreshToken, string IdToken, DateTimeOffset ExpiresAt, string[] Scopes, string Model);
public sealed record AiModel(string Id, string Name);
public sealed record UsageRecord(Guid Id, Guid Workspace, Guid Actor, string Provider, string Model, string Action, DateTimeOffset StartedAt, string Status, long InputTokens = 0, long OutputTokens = 0);
public sealed class StoreState
{
    public string HostId { get; set; } = "urn:uuid:" + Guid.NewGuid();
    public Dictionary<Guid, string> Connections { get; set; } = [];
    public Dictionary<Guid, Guid> Revisions { get; set; } = [];
    public Dictionary<Guid, WorkspaceConfig> Workspaces { get; set; } = [];
    public List<UsageRecord> Usage { get; set; } = [];
}
