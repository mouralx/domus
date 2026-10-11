using Domus.Mcp;
if (args.Contains("--healthcheck"))
{
    try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; Environment.Exit((await client.GetAsync("http://127.0.0.1:4340/health")).IsSuccessStatusCode ? 0 : 1); }
    catch { Environment.Exit(1); }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16384);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddScoped<DomusTools>();
builder.Services.AddMcpServer().WithHttpTransport(options => options.Stateless = true).WithTools<DomusTools>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        if (context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))
        { context.Response.StatusCode = 401; return; }
    }
    else
    {
        // Internal-only endpoint: require a real Domus session and a fixed project scope.
        // Membership and project access are rechecked by the owning API for every tool.
        if (string.IsNullOrEmpty(context.Request.Headers.Cookie) || !Guid.TryParse(context.Request.Headers["X-Domus-Workspace"], out _)
            || string.IsNullOrWhiteSpace(context.Request.Headers["X-Domus-Project"]) || context.Request.Headers["X-Domus-Project"].ToString().Length > 100)
        { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "A Domus session and project scope are required." }); return; }
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && origin != app.Configuration["PORTAL_PUBLIC_URL"])
        { context.Response.StatusCode = 403; return; }
        try { await context.RequestServices.GetRequiredService<DomusTools>().Validate(context.RequestAborted); }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
        { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { error = "Domus access denied or unavailable." }); return; }
    }
    await next();
});
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapMcp("/mcp");
app.Run();
