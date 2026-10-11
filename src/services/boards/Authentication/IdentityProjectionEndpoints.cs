using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Domus.Boards.Api;
public sealed record IdentityProjection(Guid Id, string Email, string Name, string? Photo, DateTimeOffset CreatedAt);
public static class IdentityProjectionEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/internal/membership/identities/{id:guid}", async (Guid id, HttpContext context, IConfiguration config, BoardsDbContext db, WorkspaceStore store) =>
        {
            var secret = config["DOMUS_IDENTITY_SYNC_SECRET"];
            if (string.IsNullOrEmpty(secret) || !long.TryParse(context.Request.Headers["X-Domus-Timestamp"], out var seconds)
                || (seconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60 || seconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60)) throw new ApiError(403, "Identity delivery rejected.");
            using var reader = new StreamReader(context.Request.Body);
            var payload = await reader.ReadToEndAsync(context.RequestAborted);
            if (payload.Length > AccountAvatar.MaxBytes * 2) throw new ApiError(413, "Identity projection too large.");
            byte[] supplied;
            try { supplied = Convert.FromHexString(context.Request.Headers["X-Domus-Signature"].ToString()); }
            catch (FormatException) { throw new ApiError(403, "Identity delivery rejected."); }
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + context.Request.Path + "\n" + payload));
            if (!CryptographicOperations.FixedTimeEquals(supplied, expected)) throw new ApiError(403, "Identity delivery rejected.");
            var source = JsonSerializer.Deserialize<IdentityProjection>(payload) ?? throw new ApiError(400, "Invalid identity projection.");
            if (source.Id != id || string.IsNullOrWhiteSpace(source.Name) || source.Name.Length > 120 || !System.Net.Mail.MailAddress.TryCreate(source.Email, out _)) throw new ApiError(400, "Invalid identity projection.");
            if (source.Photo is not null) AccountAvatar.Validate(source.Photo);
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()},0))");
            var projection = await db.Users.SingleOrDefaultAsync(x => x.Id == id);
            var changed = projection is null || projection.Name != source.Name || projection.Photo != source.Photo || projection.Email != source.Email;
            if (projection is null) { projection = new UserEntity { Id = id, CreatedAt = source.CreatedAt }; db.Users.Add(projection); }
            projection.Email = source.Email; projection.Name = source.Name; projection.Photo = source.Photo;
            projection.PhotoRequired = false; projection.PasswordHash = null; projection.TwoFactorSecret = null;
            projection.TwoFactorPendingSecret = null; projection.TwoFactorPendingExpiresAt = null;
            await db.SaveChangesAsync();
            if (changed)
            {
                // Member names are the UI's assignee identifiers; update the projection in the owning domain.
                await db.Members.Where(x => x.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, source.Name).SetProperty(x => x.Photo, source.Photo));
                await db.Workspaces.Where(x => db.Members.Any(m => m.WorkspaceId == x.Id && m.UserId == id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version + 1));
            }
            if (!await db.Workspaces.AnyAsync(x => x.OwnerId == id && x.Personal)) await store.Create(id, "My Workspace", true);
            await transaction.CommitAsync();
            return Results.NoContent();
        }).AllowAnonymous().ExcludeFromDescription();
    }
}
