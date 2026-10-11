using System.Net;
using System.Net.Http.Json;
using Automations.Core;

namespace Automations.Api;

public sealed record WorkspaceIdentity(Guid UserId, Guid WorkspaceId, bool CanManage, bool CanManagePlatform);

public sealed class DomusIdentity
{
    // Never retain cookies from another caller or follow a redirect to a login page.
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    public async Task Authenticate(HttpContext context, Database database)
    {
        var path = context.Request.Path.Value!.TrimEnd('/').ToLowerInvariant();
        if (path is "/api/login" or "/api/register" or "/api/logout" or "/api/session") throw new AppError(404, "Use o login da plataforma Domus.");
        if (string.IsNullOrEmpty(context.Request.Headers.Cookie)) throw new AppError(401, "Entre na plataforma para continuar.");
        var settings = path.StartsWith("/api/settings/", StringComparison.Ordinal);
        var workspace = context.Request.Headers["X-Domus-Workspace"].ToString();
        if (string.IsNullOrEmpty(workspace)) workspace = context.Request.Query["workspace"].ToString();
        if (!settings && workspace != "studio" && !Guid.TryParse(workspace, out _)) throw new AppError(400, "Selecione um workspace válido.");
        var origin = settings ? Environment.GetEnvironmentVariable("DOMUS_MEMBERSHIP_URL")! : Environment.GetEnvironmentVariable("DOMUS_WORK_API_URL")!;
        var endpoint = settings ? "/api/domus/platform/context" : "/api/domus/workspaces/" + Uri.EscapeDataString(workspace) + "/context";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(origin), endpoint));
        request.Headers.Host = new Uri(Environment.GetEnvironmentVariable("PORTAL_PUBLIC_URL")!).Authority;
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
        using var response = await client.SendAsync(request, context.RequestAborted);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new AppError((int)response.StatusCode, "Sessão ou acesso ao workspace inválido. Entre na plataforma e conclua a autenticação.");
        if (!response.IsSuccessStatusCode) throw new AppError(503, "Não foi possível validar a sessão da plataforma.");
        var identity = await response.Content.ReadFromJsonAsync<WorkspaceIdentity>(context.RequestAborted) ?? throw new AppError(503, "Identidade inválida.");
        if ((!settings && identity.WorkspaceId == Guid.Empty) || identity.UserId == Guid.Empty) throw new AppError(503, "Identidade inválida.");
        var management = context.Request.Method != "GET" && path is "/api/catalog/install" or "/api/import" or "/api/packages/remove";
        if (settings && !identity.CanManagePlatform) throw new AppError(403, "Só um administrador da plataforma pode gerir o repositório e o PAT globais.");
        if (management && !identity.CanManage) throw new AppError(403, "Só o proprietário pode gerir automações e definições.");
        if (!settings) await database.EnsureWorkspace(identity.WorkspaceId);
        context.Items["userId"] = settings ? identity.UserId : identity.WorkspaceId;
        context.Items["actorId"] = identity.UserId;
    }
}
