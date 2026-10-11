using Domus.Boards.Api;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var response = await client.GetAsync("http://127.0.0.1:5180/api/health/ready");
        Environment.Exit(response.IsSuccessStatusCode ? 0 : 1);
    }
    catch { Environment.Exit(1); }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();
var providers = builder.AddDomusBoards();
var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RequestSecurityMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TwoFactorEnrollmentMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/api/health", async (BoardsDbContext db) =>
{
    if (!await db.Database.CanConnectAsync()) return Results.StatusCode(503);
    return Results.Ok(new { status = "healthy", database = "PostgreSQL" });
}).AllowAnonymous().WithTags("Health");
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOMUS_MEMBERSHIP_URL"))) AuthenticationEndpoints.Map(app, providers);
IdentityProjectionEndpoints.Map(app);
PlatformBranding.Map(app);
var api = app.MapGroup("/api").RequireAuthorization();
WorkspaceEndpoints.Map(api);
AutomationContext.Map(api);
CardEndpoints.Map(api);
AssistantReadEndpoints.Map(api);
CardDataEndpoints.Map(api);
JiraEndpoints.Map(api);
JiraHostApproval.Map(api);
FileEndpoints.Map(api);
ShareEndpoints.Map(api);
InvitationEndpoints.Map(api);
app.MapGet("/api/health/ready", async (Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService health, CancellationToken cancellationToken) =>
{
    var report = await health.CheckHealthAsync(cancellationToken);
    return Results.Text(report.Status.ToString(), "text/plain", statusCode: report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? 200 : 503);
}).AllowAnonymous().WithTags("Health");
app.MapBoardsDocumentation();
await using (var scope = app.Services.CreateAsyncScope())
{
    if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().Initialize();
    else if ((await scope.ServiceProvider.GetRequiredService<BoardsDbContext>().Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Database migrations are pending. Apply them with dotnet ef database update before starting the API.");
    await app.Services.GetRequiredService<PlatformAdmins>().Initialize(app.Configuration, scope.ServiceProvider.GetRequiredService<BoardsDbContext>());
}
app.Run();
public partial class Program;
