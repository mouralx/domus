namespace Domus.Boards.Api;

// Identity changes cross the domain boundary through a durable membership outbox
// and authenticated HTTP. Implementations must never open another domain's database.
public interface IIdentityDirectory
{
    Task Sync(Guid id);
    Task Flush(Guid id);
}
