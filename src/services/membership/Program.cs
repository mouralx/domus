using Domus.Membership;
using Kanbada.Api;
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
var providers = builder.AddKanbada();
builder.Services.RemoveAll<KanbadaDbContext>();
builder.Services.AddScoped<KanbadaDbContext>(sp => new MembershipDbContext(new DbContextOptionsBuilder<KanbadaDbContext>().UseNpgsql(builder.Configuration.GetConnectionString("Postgres")!).Options));
var boardsConnection = builder.Configuration.GetConnectionString("Boards") ?? throw new InvalidOperationException("Configure ConnectionStrings:Boards.");
builder.Services.AddScoped<BoardsData>(_ => new BoardsData(new KanbadaDbContext(new DbContextOptionsBuilder<KanbadaDbContext>().UseNpgsql(boardsConnection).Options)));
builder.Services.AddScoped<WorkspaceStore>(sp => new WorkspaceStore(sp.GetRequiredService<BoardsData>().Context, new WorkspaceMapper(sp.GetRequiredService<BoardsData>().Context)));
builder.Services.AddScoped<UserDirectoryBridge>(sp => new UserDirectoryBridge(sp.GetRequiredService<KanbadaDbContext>(), sp.GetRequiredService<BoardsData>().Context));
var app = builder.Build();
app.UseExceptionHandler(); app.UseStatusCodePages();
app.UseMiddleware<RequestSecurityMiddleware>(); app.UseAuthentication(); app.UseMiddleware<TwoFactorEnrollmentMiddleware>(); app.UseAuthorization(); app.UseRateLimiter();
AuthenticationEndpoints.Map(app, providers);
app.MapGet("/api/membership/health", async (KanbadaDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { ok = true }) : Results.StatusCode(503));
app.MapGet("/api/membership/session/context", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new MembershipSession(Auth.User(ctx), ctx.User.FindFirstValue(ClaimTypes.Name)!, ctx.User.FindFirstValue(ClaimTypes.Email)!, ctx.User.HasClaim("avatar_pending", "true"), ctx.User.HasClaim("two_factor_pending", "true"), admins.Contains(Auth.User(ctx))))).RequireAuthorization();
app.MapGet("/api/domus/platform/context", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new { userId = Auth.User(ctx), canManagePlatform = admins.Contains(Auth.User(ctx)) })).RequireAuthorization();
app.MapGet("/api/platform/branding/access", (HttpContext ctx, PlatformAdmins admins) => Results.Ok(new { canManage = admins.Contains(Auth.User(ctx)) })).RequireAuthorization();
app.MapPost("/api/auth/profile", async (ProfileInput input, HttpContext ctx, KanbadaDbContext db, UserDirectoryBridge directory) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120) throw new ApiError(400, "Enter a name of 1–120 characters.");
    var user = await db.Users.SingleAsync(x => x.Id == Auth.User(ctx)); user.Name = input.Name.Trim();
    await db.SaveChangesAsync(); await directory.Sync(user.Id); return Results.NoContent();
}).RequireAuthorization();
MembershipInvitations.Map(app.MapGroup("/api").RequireAuthorization());
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KanbadaDbContext>();
    await db.Database.EnsureCreatedAsync();
    await app.Services.GetRequiredService<PlatformAdmins>().Initialize(app.Configuration, db);
}
app.Run();
public sealed record ProfileInput(string Name);
