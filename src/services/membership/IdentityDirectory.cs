using Domus.Boards.Api;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Domus.Membership;

public sealed class DirectoryEvent
{
    public Guid UserId { get; set; }
    public string Payload { get; set; } = "";
    public Guid Version { get; set; }
}
public sealed class IdentityDirectory(BoardsDbContext db, IConfiguration configuration, HttpClient client, ILogger<IdentityDirectory> logger) : IIdentityDirectory
{
    public async Task Sync(Guid id)
    {
        var profile = await db.Users.AsNoTracking().SingleAsync(user => user.Id == id);
        var row = await db.Set<DirectoryEvent>().FindAsync(id);
        if (row is null) { row = new DirectoryEvent { UserId = id }; db.Add(row); }
        row.Version = Guid.NewGuid();
        row.Payload = JsonSerializer.Serialize(new { profile.Id, profile.Email, profile.Name, profile.Photo, profile.CreatedAt });
        await db.SaveChangesAsync();
    }
    public async Task Flush(Guid id)
    {
        // PostgreSQL advisory locks serialize delivery per identity, including
        // background retries and concurrent profile updates in separate instances.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()},0))");
        var row = await db.Set<DirectoryEvent>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id);
        if (row is null) { await transaction.CommitAsync(); return; }
        try
        {
            var secret = configuration["DOMUS_IDENTITY_SYNC_SECRET"] ?? throw new InvalidOperationException("Identity sync secret is required.");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var path = $"/internal/membership/identities/{id}";
            var bytes = Encoding.UTF8.GetBytes(row.Payload);
            var signed = Encoding.UTF8.GetBytes(timestamp + "\n" + path + "\n" + row.Payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(configuration["DOMUS_BOARDS_URL"]!), path));
            request.Headers.Host = new Uri(configuration["PortalOrigin"]!).Authority;
            request.Headers.Add("X-Domus-Request", "1");
            request.Headers.Add("X-Domus-Timestamp", timestamp);
            request.Headers.Add("X-Domus-Signature", Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed)));
            request.Content = new ByteArrayContent(bytes); request.Content.Headers.ContentType = new("application/json");
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Identity delivery rejected.", null, response.StatusCode);
            await db.Set<DirectoryEvent>().Where(x => x.UserId == id && x.Version == row.Version).ExecuteDeleteAsync();
            await transaction.CommitAsync();
            // Sync can have tracked this row in the request's context before commit.
            foreach (var tracked in db.ChangeTracker.Entries<DirectoryEvent>().Where(x => x.Entity.UserId == id).ToArray()) tracked.State = EntityState.Detached;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning("Boards identity delivery pending ({ErrorType}); the durable outbox will retry.", error.GetType().Name);
            // Do not remove the outbox record. The identity update is already committed.
        }
    }
}
public sealed class DirectoryDispatcher(IServiceScopeFactory scopes, ILogger<DirectoryDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BoardsDbContext>();
                var pending = await db.Set<DirectoryEvent>().AsNoTracking().OrderBy(x => x.UserId).Select(x => x.UserId).Take(50).ToArrayAsync(stoppingToken);
                foreach (var id in pending) await scope.ServiceProvider.GetRequiredService<IIdentityDirectory>().Flush(id);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Identity outbox retry deferred ({ErrorType}).", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
