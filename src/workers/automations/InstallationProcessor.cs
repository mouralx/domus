using Automations.Core;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Automations.Worker;

public sealed class InstallationProcessor(Database database, Storage storage, PackageService packages, ILogger<InstallationProcessor> logger)
{
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(15) };
    public async Task Process(Guid id, CancellationToken ct)
    {
        var job = await database.Installation(id) ?? throw new InvalidOperationException("Installation missing.");
        try
        {
            var secret = await File.ReadAllTextAsync(Path.Combine(storage.Root,"worker-token"),ct);
            var origin = Environment.GetEnvironmentVariable("INSTALL_API_URL") ?? "http://api:4310";
            using var request = new HttpRequestMessage(HttpMethod.Get,new Uri(new Uri(origin),$"/internal/installations/{id}/archive"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",secret);
            using var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))?["error"]?.GetValue<string>() ?? "Não foi possível obter o pacote.";
                throw new AppError((int)response.StatusCode,error);
            }
            await using var content = await response.Content.ReadAsStreamAsync(ct);
            await packages.Install(job.UserId,content,job.Recipe.Digest,ct,job.Recipe.AutomationId,job.Recipe.Version);
            await database.FinishInstallation(id,"completed");
        }
        catch (OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogWarning("Installation {Id} failed: {Type}",id,error.GetType().Name);
            await database.FinishInstallation(id,"failed",error is AppError ? error.Message : "Não foi possível instalar a automação. Tente novamente.");
        }
    }
}
