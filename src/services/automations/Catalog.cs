using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Automations.Core;

namespace Automations.Api;

public static class Catalog
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid, string), Asset> Assets = new();
    public sealed record Asset(string Id, string Name, long Size, string ReleaseName, string Tag, string Url, string? Digest, string Owner, string Repository, string AutomationId, string Version);
    public sealed record Lookup(string Owner, string Repository, string AutomationId, string Version);
    public sealed record InstallInput(string AssetId);
    private static HttpRequestMessage GitHubRequest(Uri url, string? token, bool download = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("robolex/1.0");
        if (url.Host == "api.github.com")
        {
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.Accept.ParseAdd(download ? "application/octet-stream" : "application/vnd.github+json");
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }
    private static AppError GitHubError(System.Net.HttpStatusCode status, bool download = false) => status switch
    {
        System.Net.HttpStatusCode.NotFound => new(404, "Repositório ou release não encontrado. Confirme autor/repositorio. Para repositórios privados, guarde um PAT em Definições → GitHub com acesso ao repositório."),
        System.Net.HttpStatusCode.Unauthorized => new(401, "O token GitHub é inválido ou expirou. Atualize-o em Definições → GitHub."),
        System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests => new(429, "O GitHub recusou o acesso ou atingiu o limite de pedidos. Confirme as permissões do token e tente novamente mais tarde."),
        _ => new(502, download ? "Não foi possível descarregar o pacote do GitHub." : "Não foi possível consultar os releases no GitHub.")
    };
    public static void Map(WebApplication app, Database database, PackageService packages, GitHubCredentials credentials)
    {
        app.MapGet("/api/catalog", async (HttpContext context, GitHubCredentials access) =>
        {
            var user = (Guid)context.Items["userId"]!;
            var repository = await access.Repository(user) ?? throw new AppError(409, "Configure o repositório nas Definições para consultar o catálogo.");
            var parts = repository.Split('/'); var owner = parts[0]; var repo = parts[1];
            var releases = new List<JsonNode>();
            for (var page = 1; page <= 10; page++)
            {
                using var request = GitHubRequest(new Uri($"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100&page={page}"), await access.Token(user));
                using var response = await Client.SendAsync(request, context.RequestAborted); if (!response.IsSuccessStatusCode) throw GitHubError(response.StatusCode);
                var batch = JsonNode.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted))!.AsArray(); releases.AddRange(batch.Select(item => item!)); if (batch.Count < 100) break;
            }
            var assets = new List<Asset>();
            foreach (var release in releases.Where(release => !release["draft"]!.GetValue<bool>() && !release["prerelease"]!.GetValue<bool>()))
            foreach (var asset in release["assets"]!.AsArray())
            {
                var name = asset!["name"]!.GetValue<string>(); var tag = release["tag_name"]!.GetValue<string>();
                var identity = CatalogEntries.Identity(name, tag); if (identity is null) continue;
                var digest = asset["digest"]?.GetValue<string>();
                assets.Add(new Asset(asset["id"]!.ToString(), name, asset["size"]!.GetValue<long>(), release["name"]?.GetValue<string>() ?? identity.Value.Id, tag, asset["browser_download_url"]!.GetValue<string>(), digest?.StartsWith("sha256:") == true ? digest[7..] : null, owner, repo, identity.Value.Id, identity.Value.Version));
            }
            var latest = assets.GroupBy(asset => asset.AutomationId).Select(group => group.OrderByDescending(asset => System.Version.Parse(asset.Version)).ThenByDescending(asset => long.Parse(asset.Id)).First()).OrderBy(asset => asset.AutomationId).ToList();
            foreach (var key in Assets.Keys.Where(key => key.Item1 == user)) Assets.TryRemove(key, out _);
            foreach (var asset in latest) Assets[(user, asset.Id)] = asset;
            var installed = await database.Packages(user);
            var automations = new List<CatalogEntry>();
            foreach (var asset in latest)
            {
                var entry = CatalogEntries.Describe(asset.Id, asset.AutomationId, asset.Version, Regex.Replace(asset.ReleaseName, @"\s+v\d+\.\d+\.\d+$", ""), asset.Size, installed);
                if (entry.InstalledVersion is null)
                {
                    try
                    {
                        using var metadataRequest = GitHubRequest(new Uri($"https://api.github.com/repos/{owner}/{repo}/contents/src/{asset.AutomationId}/automation.json?ref={Uri.EscapeDataString(asset.Tag)}"), await access.Token(user));
                        using var metadataResponse = await Client.SendAsync(metadataRequest, context.RequestAborted);
                        if (metadataResponse.IsSuccessStatusCode)
                        {
                            var metadata = JsonNode.Parse(await metadataResponse.Content.ReadAsStringAsync(context.RequestAborted));
                            var content = metadata?["content"]?.GetValue<string>();
                            var manifest = content is null ? null : JsonNode.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content)));
                            if (manifest?["id"]?.GetValue<string>() == asset.AutomationId) entry = entry with { Name = manifest["name"]?.GetValue<string>() ?? entry.Name, Description = manifest["description"]?.GetValue<string>() ?? entry.Description };
                        }
                    }
                    catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or FormatException or InvalidOperationException) { /* Release title remains a fallback when optional metadata is unavailable. */ }
                }
                automations.Add(entry);
            }
            return Results.Ok(new { repository, automations });
        });
        app.MapPost("/api/catalog/install", async (InstallInput input, HttpContext context) =>
        {
            var user = (Guid)context.Items["userId"]!;
            if (!Assets.TryGetValue((user, input.AssetId), out var asset)) throw new AppError(400, "Consulte o catálogo novamente antes de instalar.");
            if (!string.Equals(await credentials.Repository(user), asset.Owner + "/" + asset.Repository, StringComparison.OrdinalIgnoreCase)) throw new AppError(409, "O repositório mudou. Consulte novamente o catálogo.");
            var url = new Uri(asset.Url);
            if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith($"/{asset.Owner}/{asset.Repository}/releases/download/", StringComparison.OrdinalIgnoreCase)) throw new AppError(400, "Endereço do release inválido.");
            var gate = packages.MutationGate(user); await gate.WaitAsync(context.RequestAborted);
            try
            {
                var job = await database.EnqueueInstallation(user, new(asset.Id,asset.Owner,asset.Repository,asset.AutomationId,asset.Version,asset.Digest));
                return Results.Json(new { id = job.Id, status = job.Status }, statusCode: 202);
            }
            finally { gate.Release(); }
        });
        app.MapGet("/api/installations", async (HttpContext context, Database store) => Results.Ok(await store.Installations((Guid)context.Items["userId"]!)));
        app.MapGet("/api/installations/{id:guid}", async (Guid id, HttpContext context) => Results.Ok(await database.Installation(id,(Guid)context.Items["userId"]!) ?? throw new AppError(404,"Instalação não encontrada.")));
        app.MapGet("/internal/installations/{id:guid}/archive", async (Guid id, HttpContext context, Storage storage) =>
        {
            var secret = await File.ReadAllTextAsync(Path.Combine(storage.Root,"worker-token"));
            var supplied = context.Request.Headers.Authorization.ToString();
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(supplied)),System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Bearer "+secret)))) throw new AppError(401,"Acesso interno não autorizado.");
            var job = await database.Installation(id) ?? throw new AppError(404,"Instalação não encontrada.");
            if (job.Status != "installing") throw new AppError(409,"Instalação não está em curso.");
            var asset = job.Recipe; var user = job.UserId;
            var url = new Uri($"https://api.github.com/repos/{asset.Owner}/{asset.Repository}/releases/assets/{asset.AssetId}");
            for (var redirects = 0; redirects < 5; redirects++)
            {
                using var request = GitHubRequest(url, await credentials.Token(user), download: true);
                using var response = await Client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,context.RequestAborted);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is Uri location)
                {
                    url = new Uri(url,location);
                    if (url.Scheme != "https" || url.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com") || url.UserInfo.Length > 0) throw new AppError(400,"Redirecionamento inválido.");
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw GitHubError(response.StatusCode,true);
                if (response.Content.Headers.ContentLength > PackageService.ArchiveLimit) throw new AppError(413,"O pacote é demasiado grande.");
                context.Response.ContentType = "application/zip";
                await using var content = await response.Content.ReadAsStreamAsync(context.RequestAborted); await content.CopyToAsync(context.Response.Body,context.RequestAborted); return;
            }
            throw new AppError(400,"Demasiados redirecionamentos.");
        });
    }
}
