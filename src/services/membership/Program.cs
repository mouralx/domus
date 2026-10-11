using Domus.Membership;
using Domus.Boards.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;

if (args.Contains("--healthcheck"))
{
    try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; Environment.Exit((await client.GetAsync("http://127.0.0.1:5190/api/membership/health")).IsSuccessStatusCode ? 0 : 1); }
    catch { Environment.Exit(1); }
    return;
}
var builder = WebApplication.CreateBuilder(args);
if (!Uri.TryCreate(builder.Configuration["DOMUS_BOARDS_URL"], UriKind.Absolute, out var boardsUrl) || boardsUrl.Scheme is not ("http" or "https") || (builder.Configuration["DOMUS_IDENTITY_SYNC_SECRET"]?.Length ?? 0) < 32)
    throw new InvalidOperationException("Configure the boards HTTP origin and an internal identity sync secret of at least 32 characters.");
var providers = builder.AddDomusBoards();
builder.Services.RemoveAll<BoardsDbContext>();
builder.Services.AddScoped<BoardsDbContext>(sp => new MembershipDbContext(new DbContextOptionsBuilder<BoardsDbContext>().UseNpgsql(builder.Configuration.GetConnectionString("Postgres")!).Options));
builder.Services.RemoveAll<WorkspaceStore>();
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) });
builder.Services.AddScoped<IIdentityDirectory, IdentityDirectory>();
builder.Services.AddHostedService<DirectoryDispatcher>();
var app = builder.Build();
app.UseExceptionHandler(); app.UseStatusCodePages();
app.UseMiddleware<RequestSecurityMiddleware>(); app.UseAuthentication(); app.UseMiddleware<TwoFactorEnrollmentMiddleware>(); app.UseAuthorization(); app.UseRateLimiter();
AuthenticationEndpoints.Map(app, providers);
app.MapGet("/api/membership/health", async (BoardsDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { ok = true }) : Results.StatusCode(503)).AllowAnonymous();
app.MapGet("/api/membership/session/context", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new MembershipSession(Auth.User(ctx), ctx.User.FindFirstValue(ClaimTypes.Name)!, ctx.User.FindFirstValue(ClaimTypes.Email)!, ctx.User.HasClaim("avatar_pending", "true"), ctx.User.HasClaim("two_factor_pending", "true"), admins.Contains(Auth.User(ctx))))).RequireAuthorization();
app.MapGet("/api/domus/platform/context", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new { userId = Auth.User(ctx), canManagePlatform = admins.Contains(Auth.User(ctx)) })).RequireAuthorization();
app.MapGet("/api/platform/branding/access", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new { canManage = admins.Contains(Auth.User(ctx)) })).RequireAuthorization();
app.MapPost("/api/auth/profile", async (ProfileInput input, HttpContext ctx, BoardsDbContext db, IIdentityDirectory directory) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120) throw new ApiError(400, "Enter a name of 1–120 characters.");
    await using var transaction = await db.Database.BeginTransactionAsync();
    var user = await db.Users.SingleAsync(x => x.Id == Auth.User(ctx)); user.Name = input.Name.Trim();
    await db.SaveChangesAsync(); await directory.Sync(user.Id); await transaction.CommitAsync(); await directory.Flush(user.Id); return Results.NoContent();
}).RequireAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BoardsDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS membership_directory_outbox (user_id uuid PRIMARY KEY, payload text NOT NULL, version uuid NOT NULL)");
    await app.Services.GetRequiredService<PlatformAdmins>().Initialize(app.Configuration, db);
}
app.Run();
public sealed record ProfileInput(string Name);
