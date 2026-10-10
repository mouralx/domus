using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Automations.Core;
using CoreJson = Automations.Core.Json;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Automations.Worker;

public sealed class ExecutionConsumer(Database database, Storage storage, JobQueue queue, InstallationProcessor installer, IHostApplicationLifetime lifetime, ILogger<ExecutionConsumer> logger) : BackgroundService
{
    public bool Ready { get; private set; }
    private readonly CancellationTokenSource shutdown = new();
    private readonly object logsLock = new();
    private Process? active;
    private IChannel? channel;
    private NpgsqlConnection? lease;
    private static readonly Regex AllowedEnvironment = new("^(PATH|HOME|LANG|LC_.*|TMP|TEMP|TMPDIR|DISPLAY|XAUTHORITY|WAYLAND_DISPLAY|XDG_RUNTIME_DIR|SYSTEMROOT|WINDIR|COMSPEC|PATHEXT|USERPROFILE)$", RegexOptions.IgnoreCase);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, shutdown.Token);
        try
        {
            lease = await database.Source.OpenConnectionAsync(combined.Token);
            await using var acquire = new NpgsqlCommand("SELECT pg_try_advisory_lock(4310321)", lease);
            if (await acquire.ExecuteScalarAsync(combined.Token) is not true) throw new InvalidOperationException("Only one execution worker is supported.");
            foreach (var interrupted in await database.RecoverInterrupted())
            {
                File.Delete(storage.JobFile(interrupted.User, interrupted.Id));
                File.Delete(Path.Combine(storage.RunDirectory(interrupted.User, interrupted.Id), "workspace", ".env"));
            }
            await database.RecoverInstallations();
            channel = await queue.Connection.CreateChannelAsync(cancellationToken: combined.Token);
            await channel.BasicQosAsync(0, 1, false, combined.Token);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try
                {
                    var payload = Encoding.UTF8.GetString(delivery.Body.Span); var installation = payload.StartsWith("installation:",StringComparison.Ordinal);
                    if (!Guid.TryParse(installation ? payload["installation:".Length..] : payload, out var id)) { await channel.BasicRejectAsync(delivery.DeliveryTag, false, combined.Token); return; }
                    // The claim is durable before any process starts. Redelivery never reruns a claimed execution.
                    if (installation) { if (await database.ClaimInstallation(id)) await installer.Process(id,combined.Token); }
                    else if (await database.Claim(id)) await Execute(id, combined.Token);
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, combined.Token);
                }
                catch (OperationCanceledException) when (combined.IsCancellationRequested) { }
                catch (Exception error) { logger.LogError("Execution consumer stopped: {Type}", error.GetType().Name); Ready = false; lifetime.StopApplication(); }
            };
            await channel.BasicConsumeAsync(JobQueue.QueueName, false, consumer, combined.Token);
            Ready = true;
            while (!combined.IsCancellationRequested)
            {
                await Task.Delay(1000, combined.Token);
                await using var check = new NpgsqlCommand("SELECT 1", lease); await check.ExecuteScalarAsync(combined.Token);
            }
        }
        finally { Ready = false; KillActive(); }
    }
    private void KillActive()
    {
        try { if (active is { HasExited: false }) active.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }
    private async Task Execute(Guid id, CancellationToken ct)
    {
        var execution = await database.Execution(id) ?? throw new InvalidOperationException("Execution missing.");
        var run = execution.Record; var user = execution.UserId; var directory = storage.RunDirectory(user, id); var workspace = Path.Combine(directory, "workspace");
        var job = storage.JobFile(user, id); var environmentFile = Path.Combine(workspace, ".env");
        Directory.CreateDirectory(workspace); List<string> secrets = []; Task stdout = Task.CompletedTask, stderr = Task.CompletedTask; StreamWriter? log = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(long.Parse(Environment.GetEnvironmentVariable("RUN_TIMEOUT_MS") ?? "7200000")));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        using var killOnCancel = cancel.Token.Register(KillActive);
        try
        {
            if (execution.CancelRequested) { run.Status = "cancelled"; run.Message = "Execução cancelada antes de iniciar."; return; }
            var package = await database.Package(user, run.PackageKey) ?? throw new AppError(404, "Pacote não encontrado.");
            if (!package.Compatible) throw new AppError(400, "Pacote incompatível com o worker.");
            var values = JsonNode.Parse(await File.ReadAllTextAsync(job, ct))!.AsObject();
            await RunPreparation.Validate(database, storage, user, package, values);
            var installed = storage.PackageDirectory(user, package); var manifest = package.Manifest; var config = manifest["execution"]!;
            var executable = Storage.Resolve(installed, config["executable"]!.GetValue<string>());
            if ((File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0) throw new AppError(400, "Executável inválido.");
            var start = new ProcessStartInfo(executable) { WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var key in start.Environment.Keys.ToList()) if (!AllowedEnvironment.IsMatch(key)) start.Environment.Remove(key);
            start.Environment["HOME"] = workspace;
            foreach (var argument in config["arguments"]!.AsArray()) start.ArgumentList.Add(argument!.GetValue<string>());
            start.Environment[config["homeEnvironmentVariable"]!.GetValue<string>()] = workspace;
            foreach (var pair in config["packagePaths"]!.AsObject()) start.Environment[pair.Key] = Storage.Resolve(installed, pair.Value!.GetValue<string>());
            if (config["stateDirectoryArgument"] is not null) { start.ArgumentList.Add(config["stateDirectoryArgument"]!.GetValue<string>()); start.ArgumentList.Add(Path.Combine(workspace, ".state")); }
            var envValues = new Dictionary<string, string>();
            foreach (var input in manifest["inputs"]!.AsArray())
            {
                var name = input!["id"]!.GetValue<string>(); var value = values[name] ?? input["default"]; if (value is null || value.ToJsonString() == "\"\"") continue;
                var text = input["type"]!.GetValue<string>() == "integer" ? value.GetValue<long>().ToString(System.Globalization.CultureInfo.InvariantCulture) : value.GetValue<string>();
                if (input["type"]!.GetValue<string>() == "file")
                {
                    var upload = await database.Upload(user, Guid.Parse(text)) ?? throw new AppError(400, "Ficheiro não encontrado.");
                    var original = Storage.Resolve(Path.Combine(storage.Workspace(user), "uploads"), upload.Filename);
                    text = Path.Combine(workspace, name + Path.GetExtension(upload.Name).ToLowerInvariant()); File.Copy(original, text, false);
                }
                var binding = input["binding"]!;
                if (binding["argument"] is not null) { start.ArgumentList.Add(binding["argument"]!.GetValue<string>()); start.ArgumentList.Add(text); }
                else { var key = binding["envFileKey"]!.GetValue<string>(); envValues[key] = text; start.Environment[key] = text; }
            }
            run.ResourceRoots = config["workspaceResources"]!.AsArray().Select(item => item!.GetValue<string>()).ToList();
            foreach (var relative in run.ResourceRoots)
            {
                var target = Storage.Resolve(workspace, relative); if (File.Exists(target) || Directory.Exists(target)) throw new AppError(400, "Recurso em conflito com um ficheiro de entrada.");
                RunPreparation.CopyResource(Storage.Resolve(installed, relative), target);
            }
            var envJson = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await Storage.PrivateFile(environmentFile, string.Join('\n', envValues.Select(pair => pair.Key + "=" + JsonSerializer.Serialize(pair.Value, envJson))) + "\n", ct);
            secrets = envValues.Values.Where(value => value.Length > 0).SelectMany(value => new[] { value, JsonSerializer.Serialize(value, envJson)[1..^1] }).Distinct().OrderByDescending(value => value.Length).ToList();
            start.Environment["AUTOMATIONS_PORTAL_EVENTS"] = "1";
            run.Status = "running"; await Save(run, user);
            active = new Process { StartInfo = start }; cancel.Token.ThrowIfCancellationRequested(); active.Start();
            log = new StreamWriter(Path.Combine(directory, "execution.log")); var logBytes = 0;
            async Task Pump(StreamReader reader)
            {
                while (await reader.ReadLineAsync() is string line)
                {
                    if (line.StartsWith("@@automation-event@@", StringComparison.Ordinal))
                    {
                        try { var evt = JsonNode.Parse(line["@@automation-event@@".Length..]); var status = evt?["state"]?.GetValue<string>(); if (evt?["type"]?.GetValue<string>() == "interaction" && status is "awaiting-user" or "running") { lock (logsLock) run.Status = status; continue; } } catch (JsonException) { }
                    }
                    foreach (var secret in secrets) line = line.Replace(secret, "[oculto]", StringComparison.Ordinal);
                    line = line[..Math.Min(line.Length, 16384)];
                    lock (logsLock) { run.Logs.Add(line); if (run.Logs.Count > 300) run.Logs.RemoveAt(0); if ((logBytes += Encoding.UTF8.GetByteCount(line)) < 10 * 1024 * 1024) log.WriteLine(line); }
                }
            }
            stdout = Pump(active.StandardOutput); stderr = Pump(active.StandardError);
            while (!active.HasExited)
            {
                var current = await database.Execution(id);
                if (current?.CancelRequested == true) { cancel.Cancel(); KillActive(); }
                await Save(run, user);
                await Task.Delay(250, CancellationToken.None);
                if (cancel.IsCancellationRequested) KillActive();
            }
            await active.WaitForExitAsync(CancellationToken.None); await Task.WhenAll(stdout, stderr);
            run.ExitCode = active.ExitCode; run.Status = cancel.IsCancellationRequested ? "cancelled" : active.ExitCode == 0 ? "completed" : "failed";
            run.Message = timeout.IsCancellationRequested ? "Limite de tempo excedido." : run.Status == "completed" ? "Execução concluída." : run.Status == "cancelled" ? "Execução cancelada." : "A execução terminou com erro.";
        }
        catch (OperationCanceledException) { KillActive(); run.Status = "cancelled"; run.Message = "Execução cancelada."; }
        catch (Exception error) { KillActive(); run.Status = "failed"; run.Message = error is AppError ? error.Message : "Não foi possível concluir a execução."; logger.LogWarning("Execution {Id} failed: {Type}", run.Id, error.GetType().Name); }
        finally
        {
            KillActive(); if (active is not null) { try { await active.WaitForExitAsync(CancellationToken.None); } catch (InvalidOperationException) { } active.Dispose(); active = null; }
            await Task.WhenAll(stdout, stderr); if (log is not null) await log.DisposeAsync();
            if (!Storage.Terminal(run.Status)) run.Status = "failed";
            File.Delete(environmentFile); File.Delete(job); run.EndedAt = DateTimeOffset.UtcNow;
            await Save(run, user);
        }
    }
    private async Task Save(RunRecord run, Guid user)
    {
        RunRecord copy; lock (logsLock) copy = CoreJson.Read<RunRecord>(CoreJson.Write(run));
        await database.SaveRun(copy);
        var file = Path.Combine(storage.RunDirectory(user, run.Id), "run.json"); var temp = file + ".tmp"; Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllTextAsync(temp, CoreJson.Write(copy)); File.Move(temp, file, true);
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Ready = false; shutdown.Cancel(); KillActive(); await base.StopAsync(cancellationToken);
        if (channel is not null) await channel.DisposeAsync(); if (lease is not null) await lease.DisposeAsync();
    }
}
