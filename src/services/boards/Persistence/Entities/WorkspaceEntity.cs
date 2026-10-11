namespace Domus.Boards.Api;

public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public bool Personal { get; set; }
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
