using Microsoft.EntityFrameworkCore;

namespace Kanbada.Api;

// Board records retain public identity projections for existing relational references.
// Passwords, sessions and authenticator secrets never enter these projections.
public sealed class UserDirectoryBridge(KanbadaDbContext identity, KanbadaDbContext boards)
{
    public async Task Sync(Guid id)
    {
        var source = await identity.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var projection = await boards.Users.SingleOrDefaultAsync(x => x.Id == id);
        if (projection is null) { projection = new UserEntity { Id = id, CreatedAt = source.CreatedAt }; boards.Users.Add(projection); }
        projection.Email = source.Email; projection.Name = source.Name; projection.Photo = source.Photo;
        projection.PhotoRequired = false; projection.PasswordHash = null; projection.TwoFactorSecret = null;
        projection.TwoFactorPendingSecret = null; projection.TwoFactorPendingExpiresAt = null;
        await boards.SaveChangesAsync();
        await boards.Members.Where(x => x.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Photo, source.Photo));
        await boards.Workspaces.Where(x => boards.Members.Any(m => m.WorkspaceId == x.Id && m.UserId == id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version + 1));
    }
}
