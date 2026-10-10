using Automations.Core;
using Automations.Worker;

if (args.Contains("--healthcheck"))
{
    try { using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }; var response = await client.GetAsync("http://127.0.0.1:4320/health"); Environment.Exit(response.IsSuccessStatusCode ? 0 : 1); }
    catch { Environment.Exit(1); }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Database(Database.ConnectionString()));
builder.Services.AddSingleton(new Storage(Environment.GetEnvironmentVariable("AUTOMATIONS_DATA_DIR") ?? "/data"));
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton(sp => new PackageService(sp.GetRequiredService<Database>(),sp.GetRequiredService<Storage>(),Path.Combine(AppContext.BaseDirectory,"schemas","automation.schema.json")));
builder.Services.AddSingleton<InstallationProcessor>();
builder.Services.AddSingleton<ExecutionConsumer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ExecutionConsumer>());
var app = builder.Build();
await app.Services.GetRequiredService<Database>().Initialize();
await app.Services.GetRequiredService<JobQueue>().Initialize();
app.MapGet("/health", async (Database database, JobQueue queue, ExecutionConsumer consumer, CancellationToken ct) => consumer.Ready && queue.Healthy && await database.Healthy(ct) ? Results.Ok(new { ok = true }) : Results.StatusCode(503));
await app.RunAsync();
