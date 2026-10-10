namespace Automations.Core;

public sealed record InstallationRecipe(string AssetId, string Owner, string Repository, string AutomationId, string Version, string? Digest);
public sealed record InstallationJob(Guid Id, Guid UserId, string Status, InstallationRecipe Recipe, string? Error, DateTimeOffset CreatedAt);
