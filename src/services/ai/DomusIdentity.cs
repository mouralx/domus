using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Domus.Ai;

public sealed class DomusIdentity(IConfiguration configuration, HttpClient client)
{
    public async Task<JsonObject> Get(HttpContext context, string origin, string path)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(origin), path));
        request.Headers.Host = new Uri(configuration["PORTAL_PUBLIC_URL"] ?? "http://localhost:8088").Authority;
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
        using var response = await client.SendAsync(request, timeout.Token);
        if ((int)response.StatusCode is 401 or 403 or 404) throw new AiError((int)response.StatusCode, "Sessão ou acesso inválido. Conclua a autenticação na plataforma.");
        if (!response.IsSuccessStatusCode) throw new AiError(503, "Não foi possível validar o acesso ao Domus.");
        return await response.Content.ReadFromJsonAsync<JsonObject>(timeout.Token) ?? throw new AiError(503, "Resposta inválida do Domus.");
    }
    public async Task<Identity> Authenticate(HttpContext context, bool platform)
    {
        if (string.IsNullOrEmpty(context.Request.Headers.Cookie)) throw new AiError(401, "Entre na plataforma para continuar.");
        var workspace = context.Request.Headers["X-Domus-Workspace"].ToString();
        if (!platform && workspace != "studio" && !Guid.TryParse(workspace, out _)) throw new AiError(400, "Selecione um workspace válido.");
        var origin = configuration[platform ? "DOMUS_MEMBERSHIP_URL" : "DOMUS_WORK_API_URL"] ?? throw new AiError(503, "O serviço de IA não está configurado.");
        var body = await Get(context, origin, platform ? "/api/domus/platform/context" : "/api/domus/workspaces/" + Uri.EscapeDataString(workspace) + "/context");
        var actor = new Identity(body["userId"]!.GetValue<Guid>(), body["workspaceId"]?.GetValue<Guid>() ?? Guid.Empty, body["canManage"]?.GetValue<bool>() ?? false, body["canManagePlatform"]?.GetValue<bool>() ?? false);
        if (actor.UserId == Guid.Empty || (!platform && actor.WorkspaceId == Guid.Empty)) throw new AiError(503, "Identidade inválida.");
        if (platform && !actor.CanManagePlatform) throw new AiError(403, "Só um administrador da plataforma pode gerir provedores de IA.");
        return actor;
    }
    public Task<JsonObject> Project(HttpContext context, Guid workspace, string project) => Get(context, configuration["DOMUS_WORK_API_URL"]!, $"/api/domus/workspaces/{workspace}/projects/{Uri.EscapeDataString(project)}/context?includeMetrics=false");
    public Task<JsonObject> CardAccess(HttpContext context, Guid workspace, string project, string[] ids) => Get(context, configuration["DOMUS_WORK_API_URL"]!, $"/api/domus/workspaces/{workspace}/projects/{Uri.EscapeDataString(project)}/cards/access?ids=" + Uri.EscapeDataString(string.Join(',', ids)));
    public Task<JsonObject> Workspace(HttpContext context, Guid workspace) => Get(context, configuration["DOMUS_WORK_API_URL"]!, $"/api/workspaces/{workspace}");
    public Task<JsonObject> Card(HttpContext context, Guid workspace, string card) => Get(context,
        configuration["DOMUS_WORK_API_URL"]!, $"/api/workspaces/{workspace}/cards/{Uri.EscapeDataString(card)}");
}
