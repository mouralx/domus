using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Automations.Core;
using CoreJson = Automations.Core.Json;
using Automations.Api;

if (args.Contains("--healthcheck"))
{
    try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; var response = await client.GetAsync("http://127.0.0.1:4310/health"); Environment.Exit(response.IsSuccessStatusCode ? 0 : 1); }
    catch { Environment.Exit(1); }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = PackageService.ArchiveLimit);
builder.Services.AddSingleton(new Database(Database.ConnectionString()));
builder.Services.AddSingleton(new Storage(Environment.GetEnvironmentVariable("AUTOMATIONS_DATA_DIR") ?? "/data"));
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton<DomusIdentity>();
builder.Services.AddSingleton<ExecutionFiles>();
builder.Services.AddSingleton(sp => new PackageService(sp.GetRequiredService<Database>(), sp.GetRequiredService<Storage>(), Path.Combine(AppContext.BaseDirectory, "schemas", "automation.schema.json")));
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddSingleton<PasswordHasher<string>>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var keyDirectory = Environment.GetEnvironmentVariable("ENCRYPTION_KEY_DIR") ?? Path.Combine(Environment.GetEnvironmentVariable("AUTOMATIONS_DATA_DIR") ?? "/data", "api-keys");
Directory.CreateDirectory(keyDirectory);
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
builder.Services.AddDataProtection().SetApplicationName("Robolex").PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
builder.Services.AddSingleton<GitHubTokenProtector>();
builder.Services.AddSingleton<GitHubCredentials>();
var app = builder.Build();
var database = app.Services.GetRequiredService<Database>(); var queue = app.Services.GetRequiredService<JobQueue>();
await database.Initialize(); await queue.Initialize();
await app.Services.GetRequiredService<GitHubCredentials>().MigratePlatform(app.Logger);
var storage = app.Services.GetRequiredService<Storage>();
var packages = app.Services.GetRequiredService<PackageService>(); var files = app.Services.GetRequiredService<ExecutionFiles>();
var hasher = app.Services.GetRequiredService<PasswordHasher<string>>();
var publicOrigin = new Uri(Environment.GetEnvironmentVariable("PORTAL_PUBLIC_URL") ?? "http://localhost:8080").GetLeftPart(UriPartial.Authority);
string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
Guid User(HttpContext context) => (Guid)context.Items["userId"]!;
void Credentials(string username, string password)
{
    if (username.Trim().Length is < 3 or > 80 || password.Length is < 12 or > 1024) throw new AppError(400, "Escolha um utilizador entre 3 e 80 caracteres e uma palavra-passe entre 12 e 1024 caracteres.");
}
async Task SignIn(HttpContext context, Account user)
{
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); await database.Session(Hash(token), user.Id);
    context.Response.Cookies.Append("portal_session", token, new CookieOptions { HttpOnly = true, Secure = publicOrigin.StartsWith("https:"), SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(12) });
}
app.Use(async (context, next) =>
{
    try
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            && (context.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))) throw new AppError(401, "Health probes are local-only.");
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var secret = await File.ReadAllTextAsync(Path.Combine(storage.Root, "worker-token"));
            var supplied = context.Request.Headers.Authorization.ToString();
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes("Bearer " + secret)))) throw new AppError(401, "Acesso interno não autorizado.");
        }
        // Only technical health and the bearer-authenticated worker route bypass user authentication.
        if (!context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase) && !context.Request.Path.StartsWithSegments("/internal"))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (context.Request.Method != "GET" && context.Request.Headers.Origin.ToString() != publicOrigin) throw new AppError(403, "Origem não autorizada.");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOMUS_WORK_API_URL")))
            {
                await context.RequestServices.GetRequiredService<DomusIdentity>().Authenticate(context, database);
            }
            else
            {
            var cookie = context.Request.Cookies["portal_session"];
            if (cookie is not null) context.Items["userId"] = await database.Authenticate(Hash(cookie));
            if (context.Request.Path != "/api/session" && context.Request.Path != "/api/register" && context.Request.Path != "/api/login" && context.Items["userId"] is not Guid) throw new AppError(401, "Inicie sessão.");
            }
        }
        await next(context);
    }
    catch (AppError error) { if (!context.Response.HasStarted) { context.Response.StatusCode = error.Status; await context.Response.WriteAsJsonAsync(new { error = error.Message }); } }
    catch (Exception error) { app.Logger.LogError("Request failed: {Type}", error.GetType().Name); if (error is Npgsql.PostgresException postgres) app.Logger.LogError("PostgreSQL {Code}: {Message}", postgres.SqlState, postgres.MessageText); if (!context.Response.HasStarted) { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = "Não foi possível concluir o pedido." }); } }
});
app.UseRateLimiter();
app.MapGet("/health", async (CancellationToken ct) => await database.Healthy(ct) && queue.Healthy ? Results.Ok(new { ok = true }) : Results.StatusCode(503));
app.MapGet("/api/session", (HttpContext context) => Results.Ok(new { authenticated = context.Items["userId"] is Guid, needsSetup = false }));
app.MapPost("/api/register", async (CredentialsInput input, HttpContext context) =>
{
    Credentials(input.Username ?? "", input.Password ?? ""); var user = await database.Register(input.Username!, hasher.HashPassword(input.Username!, input.Password!)); await SignIn(context, user); return Results.Json(new { ok = true }, statusCode: 201);
}).RequireRateLimiting("auth");
app.MapPost("/api/login", async (CredentialsInput input, HttpContext context) =>
{
    var user = await database.FindAccount((input.Username ?? "").Trim().ToLowerInvariant());
    if (user is null || input.Password is null || input.Password.Length > 1024 || hasher.VerifyHashedPassword(user.Username, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed) throw new AppError(401, "Credenciais incorretas.");
    await SignIn(context, user); return Results.Ok(new { ok = true });
}).RequireRateLimiting("auth");
app.MapPost("/api/logout", (Delegate)(async (HttpContext context) => { await database.Logout(Hash(context.Request.Cookies["portal_session"]!)); context.Response.Cookies.Delete("portal_session", new CookieOptions { Path = "/", Secure = publicOrigin.StartsWith("https:"), SameSite = SameSiteMode.Strict }); return Results.Ok(new { ok = true }); }));
app.MapGet("/api/status", (Delegate)(async (HttpContext context) => Results.Ok(new { platform = Platform.Os, arch = Platform.Arch, packages = await database.Packages(User(context)), runs = await database.Runs(User(context)), metrics = await database.Metrics(User(context)) })));
app.MapPost("/api/import", (Delegate)(async (HttpContext context) => Results.Json(await packages.Install(User(context), context.Request.Body, null, context.RequestAborted), statusCode: 201)));
app.MapPost("/api/packages/remove", async (RemovePackageInput input, HttpContext context) =>
{
    var gate = packages.MutationGate(User(context)); await gate.WaitAsync(context.RequestAborted);
    try
    {
    var user = User(context); var package = await database.RemovePackage(user, input.PackageKey ?? "");
    var directory = storage.PackageDirectory(user, package);
    try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    catch (IOException) { app.Logger.LogWarning("Removed package metadata; package files could not be cleaned up."); }
    catch (UnauthorizedAccessException) { app.Logger.LogWarning("Removed package metadata; package files could not be cleaned up."); }
    return Results.Ok(new { ok = true });
    }
    finally { gate.Release(); }
});
app.MapPost("/api/uploads", (Delegate)(async (HttpContext context) => Results.Json(await packages.Upload(User(context), context.Request.Body, context.Request.Query["name"].ToString(), context.RequestAborted), statusCode: 201)));
app.MapGet("/api/template", (Delegate)(async (HttpContext context) =>
{
    var pkg = await database.Package(User(context), context.Request.Query["packageKey"].ToString()) ?? throw new AppError(404, "Pacote não encontrado.");
    var input = pkg.Manifest["inputs"]!.AsArray().FirstOrDefault(input => input!["id"]!.GetValue<string>() == context.Request.Query["inputId"].ToString());
    if (input?["type"]?.GetValue<string>() != "file" || input["columns"] is not JsonArray columns || columns.Count == 0) throw new AppError(404, "Este campo não tem modelo Excel.");
    return Results.File(PackageService.Template(input), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", pkg.Manifest["id"]!.GetValue<string>() + ".modelo.xlsx");
}));
app.MapPost("/api/runs", async (RunInput input, HttpContext context) =>
{
    var user = User(context); var package = await database.Package(user, input.PackageKey ?? "") ?? throw new AppError(404, "Pacote não encontrado.");
    if (!package.Compatible) throw new AppError(400, "O pacote não é compatível com o servidor.");
    if (input.Values is null) throw new AppError(400, "Preencha os dados da execução.");
    await RunPreparation.Validate(database, storage, user, package, input.Values);
    var run = new RunRecord { InitiatedBy = context.Items["actorId"] as Guid?, Id = Guid.NewGuid(), PackageKey = package.Key, Name = package.Manifest["name"]!.GetValue<string>() };
    var job = storage.JobFile(user, run.Id); await Storage.PrivateFile(job, input.Values.ToJsonString(), context.RequestAborted);
    try { await database.Enqueue(user, run); } catch { File.Delete(job); throw; }
    return Results.Json(run, statusCode: 201);
});
app.MapGet("/api/runs/{id:guid}", async (Guid id, HttpContext context) =>
{
    var execution = await database.Execution(id, User(context)) ?? throw new AppError(404, "Execução não encontrada.");
    var record = JsonNode.Parse(CoreJson.Write(execution.Record))!.AsObject(); record["files"] = System.Text.Json.JsonSerializer.SerializeToNode(Storage.Terminal(execution.Status) ? files.List(User(context), execution.Record) : [], CoreJson.Options); return Results.Json(record);
});
app.MapPost("/api/runs/{id:guid}/cancel", async (Guid id, HttpContext context) => { if (await database.Execution(id, User(context)) is null) throw new AppError(404, "Execução não encontrada."); await database.Cancel(id, User(context)); return Results.Ok(new { ok = true }); });
app.MapGet("/api/runs/{id:guid}/files/{handle}", async (Guid id, string handle, HttpContext context) =>
{
    var execution = await database.Execution(id, User(context)) ?? throw new AppError(404, "Execução não encontrada.");
    if (!Storage.Terminal(execution.Status)) throw new AppError(409, "A execução ainda está em curso.");
    var file = files.Resolve(User(context), execution.Record, handle); return Results.File(file, "application/octet-stream", Path.GetFileName(file), enableRangeProcessing: true);
});
app.MapGet("/api/runs/{id:guid}/results.zip", async (Guid id, HttpContext context) =>
{
    var execution = await database.Execution(id, User(context)) ?? throw new AppError(404, "Execução não encontrada.");
    if (!Storage.Terminal(execution.Status)) throw new AppError(409, "A execução ainda está em curso.");
    var archive = await files.Archive(User(context), execution.Record, context.RequestAborted);
    context.Response.OnCompleted(() => { File.Delete(archive); return Task.CompletedTask; });
    return Results.File(archive, "application/zip", $"domus-resultados-{id}.zip", enableRangeProcessing: false);
});
var workerTokenFile = Path.Combine(storage.Root,"worker-token"); Directory.CreateDirectory(storage.Root);
try { await Storage.PrivateFile(workerTokenFile,Convert.ToHexString(RandomNumberGenerator.GetBytes(32))); } catch (IOException) when (File.Exists(workerTokenFile)) { }
McpReadEndpoints.Map(app);
GitHubCredentials.Map(app);
Catalog.Map(app, database, packages, app.Services.GetRequiredService<GitHubCredentials>());
app.MapFallback(() => Results.NotFound(new { error = "Não encontrado." }));
await app.RunAsync();

record CredentialsInput(string? Username, string? Password);
record RunInput(string? PackageKey, JsonObject? Values);

record RemovePackageInput(string? PackageKey);
