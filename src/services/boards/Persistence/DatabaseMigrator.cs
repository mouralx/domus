using Microsoft.EntityFrameworkCore;

namespace Domus.Boards.Api;

public sealed class DatabaseMigrator(BoardsDbContext db)
{
    public Task Initialize(CancellationToken cancellationToken = default) => db.Database.MigrateAsync(cancellationToken);
}
