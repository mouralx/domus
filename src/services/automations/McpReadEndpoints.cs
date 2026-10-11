using System.Text.Json.Nodes;
using Automations.Core;

namespace Automations.Api;

public static class McpReadEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/mcp/status", async (HttpContext ctx, Database db) => Results.Ok(new { platform = Platform.Os, arch = Platform.Arch, metrics = await db.Metrics(User(ctx)), totals = await db.McpCounts(User(ctx)), collections = new[] { "packages", "runs", "installations" } }));
        app.MapGet("/api/mcp/list", async (string kind, int? offset, int? limit, HttpContext ctx, Database db) =>
        {
            var start = offset ?? 0; var count = limit ?? 10;
            if (start < 0 || count is < 1 or > 10) throw new AppError(400, "Página inválida.");
            return Results.Ok(await db.McpList(User(ctx), kind, start, count));
        });
        app.MapGet("/api/mcp/data", async (string kind, string id, int? itemOffset, int? itemLimit, int? textOffset, int? textLength, HttpContext ctx, Database db, ExecutionFiles files, Storage storage) =>
        {
            var user = User(ctx); var start = itemOffset ?? 0; var count = itemLimit ?? 10; var offset = textOffset ?? 0; var length = textLength ?? 8000;
            if (start < 0 || count is < 1 or > 10 || offset < 0 || length is < 1 or > 8000) throw new AppError(400, "Página ou intervalo inválido.");
            object data; int total = 1;
            if (kind == "package") data = await db.Package(user, id) ?? throw new AppError(404, "Automação não encontrada.");
            else if (Guid.TryParse(id, out var key) && kind == "installation") data = await db.Installation(key, user) ?? throw new AppError(404, "Instalação não encontrada.");
            else if (Guid.TryParse(id, out key) && kind is "run" or "logs" or "logFile" or "files")
            {
                var execution = await db.Execution(key, user) ?? throw new AppError(404, "Execução não encontrada.");
                var run = execution.Record;
                if (kind == "run") data = new { run.Id, run.InitiatedBy, run.PackageKey, run.Name, run.Status, run.StartedAt, run.EndedAt, run.ExitCode, run.Message, execution.CancelRequested, logCount = run.Logs.Count, resultsAvailable = Storage.Terminal(run.Status) };
                else if (kind == "logFile")
                {
                    var log = Path.Combine(storage.RunDirectory(user, key), "execution.log");
                    if (!File.Exists(log)) data = new { available = false, text = "" };
                    else { if ((File.GetAttributes(log) & FileAttributes.ReparsePoint) != 0) throw new AppError(404, "Registo indisponível."); data = new { available = true, text = await File.ReadAllTextAsync(log, ctx.RequestAborted), retainedLogLimitBytes = 10 * 1024 * 1024 }; }
                }
                else if (kind == "logs") { total = run.Logs.Count; data = run.Logs.Skip(start).Take(count).ToArray(); }
                else { if (!Storage.Terminal(run.Status)) throw new AppError(409, "A execução ainda está em curso."); var results = files.Enumerate(user, run); total = results.Count(); data = results.Skip(start).Take(count).ToArray(); }
            }
            else throw new AppError(400, "Tipo ou identificador inválido.");
            var json = Automations.Core.Json.Write(data);
            if (offset > json.Length) throw new AppError(400, "Intervalo fora da página.");
            var end = Math.Min(json.Length, offset + length);
            return Results.Ok(new { kind, id, itemOffset = start, totalItems = total, nextItemOffset = start + count < total ? (int?)(start + count) : null, textOffset = offset, totalCharacters = json.Length, nextTextOffset = end < json.Length ? (int?)end : null, data = json[offset..end] });
        });
        app.MapGet("/api/mcp/runs/{id:guid}/files/{handle}", async (Guid id, string handle, long? offset, int? length, HttpContext ctx, Database db, ExecutionFiles files, CancellationToken ct) =>
        {
            var user = User(ctx); var start = offset ?? 0; var count = length ?? 12000;
            if (start < 0 || count is < 1 or > 12000) throw new AppError(400, "Intervalo inválido.");
            var execution = await db.Execution(id, user) ?? throw new AppError(404, "Execução não encontrada.");
            if (!Storage.Terminal(execution.Status)) throw new AppError(409, "A execução ainda está em curso.");
            var file = files.Resolve(user, execution.Record, handle);
            await using var input = File.OpenRead(file);
            if (start > input.Length) throw new AppError(400, "Intervalo fora do ficheiro.");
            input.Position = start; var buffer = new byte[(int)Math.Min(count, input.Length - start)]; await input.ReadExactlyAsync(buffer, ct);
            return Results.Ok(new { name = Path.GetFileName(file), size = input.Length, offset = start, nextOffset = input.Position < input.Length ? (long?)input.Position : null, encoding = "base64", data = Convert.ToBase64String(buffer) });
        });
    }
    private static Guid User(HttpContext ctx) => (Guid)ctx.Items["userId"]!;
}
