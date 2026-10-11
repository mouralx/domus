using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace Domus.Mcp;

[McpServerToolType]
public sealed class DomusTools(IHttpContextAccessor accessor, IConfiguration configuration, HttpClient client)
{
    private string Workspace => accessor.HttpContext!.Request.Headers["X-Domus-Workspace"].ToString();
    private string Project => accessor.HttpContext!.Request.Headers["X-Domus-Project"].ToString();
    private string ProjectPath => $"/api/domus/workspaces/{Uri.EscapeDataString(Workspace)}/projects/{Uri.EscapeDataString(Project)}/";
    private Task<string> Read(string suffix, CancellationToken ct) => Send("boards", HttpMethod.Get, ProjectPath + suffix, null, ct);
    private async Task<string> Send(string service, HttpMethod method, string path, JsonObject? body, CancellationToken ct)
    {
        var context = accessor.HttpContext ?? throw new InvalidOperationException("Missing request scope.");
        var setting = service switch
        {
            "boards" => "DOMUS_WORK_API_URL", "membership" => "DOMUS_MEMBERSHIP_URL",
            "automations" => "DOMUS_AUTOMATIONS_URL", "ai" => "DOMUS_AI_URL",
            _ => throw new InvalidOperationException("Unknown service.")
        };
        var endpoint = configuration[setting] ?? throw new InvalidOperationException("Service endpoint is not configured.");
        using var request = new HttpRequestMessage(method, new Uri(new Uri(endpoint), path));
        var publicOrigin = new Uri(configuration["PORTAL_PUBLIC_URL"]!);
        request.Headers.Host = publicOrigin.Authority;
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
        request.Headers.TryAddWithoutValidation("X-Domus-Workspace", Workspace);
        request.Headers.TryAddWithoutValidation("X-Domus-Request", "1");
        request.Headers.TryAddWithoutValidation("Origin", publicOrigin.GetLeftPart(UriPartial.Authority));
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Domus {service} operation failed ({(int)response.StatusCode}).", null, response.StatusCode);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return "{\"ok\":true}";
        await response.Content.LoadIntoBufferAsync(65536, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        var data = JsonNode.Parse(raw);
        return (data is JsonObject ? data : new JsonObject { ["items"] = data }).ToJsonString();
    }
    public Task<string> Validate(CancellationToken ct) => Read("context?includeMetrics=false", ct);
    [McpServerTool(Name = "get_project_metrics", ReadOnly = true), Description("Read the selected project's identity, current member and exact aggregate counts across all its cards. Use for progress and count questions; no card list is loaded.")]
    public Task<string> Metrics(CancellationToken cancellationToken) => Read("context", cancellationToken);
    [McpServerTool(Name = "search_cards", ReadOnly = true), Description("Search all cards in the selected project by title/description keywords and optional filters. Extract a few relevant keywords, rather than the full question. Returns a bounded page and next cursor; an empty keyword query lists cards. Follow cursors or refine the search when needed. Null filters mean unrestricted. Never assume one page is exhaustive.")]
    public Task<string> Search([Description("Title/description keywords, or null to list cards.")] string? query,
        [Description("Exact status name or null.")] string? status, [Description("High, Medium, Low, or null.")] string? priority,
        [Description("Completion filter, or null.")] bool? completed, [Description("True for overdue open cards, or null.")] bool? overdue,
        [Description("Exact member name, or null.")] string? assignee, [Description("Next cursor from the previous page, or null.")] string? cursor,
        [Description("Page size from 1 to 50, normally 20.")] int limit, CancellationToken cancellationToken)
    {
        var pairs = new Dictionary<string, string?> { ["q"] = query, ["status"] = status, ["priority"] = priority, ["completed"] = completed?.ToString().ToLowerInvariant(), ["overdue"] = overdue?.ToString().ToLowerInvariant(), ["assignee"] = assignee, ["cursor"] = cursor, ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        var qs = string.Join('&', pairs.Where(p => p.Value is not null).Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!)));
        return Read("cards/search?" + qs, cancellationToken);
    }
    [McpServerTool(Name = "get_card", ReadOnly = true), Description("Get current details of one card found by search_cards. Includes bounded description, comments and checklist excerpts, plus exact checklist totals. Access is restricted to the selected project.")]
    public Task<string> Card([Description("Exact card ID returned by search_cards.")] string cardId, CancellationToken cancellationToken) => Read("cards/" + Uri.EscapeDataString(cardId) + "/context", cancellationToken);

    [McpServerTool(Name = "create_card", ReadOnly = false, Destructive = false), Description("Create a card in the selected project. Call only for an explicit, reviewed user action. Identity and audit metadata are set by boards.")]
    public Task<string> CreateCard(string title, string description, string? status, string priority, CancellationToken cancellationToken) =>
        Send("boards", HttpMethod.Post, $"/api/workspaces/{Uri.EscapeDataString(Workspace)}/cards", new JsonObject { ["title"] = title, ["description"] = description, ["project"] = Project, ["status"] = status, ["priority"] = priority }, cancellationToken);

    [McpServerTool(Name = "get_my_profile", ReadOnly = true), Description("Read the currently authenticated member's profile and enrollment state from membership.")]
    public Task<string> Profile(CancellationToken cancellationToken) => Send("membership", HttpMethod.Get, "/api/membership/session/context", null, cancellationToken);
    [McpServerTool(Name = "update_my_profile", ReadOnly = false, Destructive = false), Description("Change only the current member's display name after explicit user review. Cannot change roles, passwords or another member.")]
    public Task<string> UpdateProfile(string name, CancellationToken cancellationToken) => Send("membership", HttpMethod.Post, "/api/auth/profile", new JsonObject { ["name"] = name }, cancellationToken);

    [McpServerTool(Name = "get_automation_status", ReadOnly = true), Description("Read installed packages, runs and metrics for the selected workspace. Large results may require a more specific run lookup.")]
    public Task<string> AutomationStatus(CancellationToken cancellationToken) => Send("automations", HttpMethod.Get, "/api/status", null, cancellationToken);
    [McpServerTool(Name = "get_automation_catalog", ReadOnly = true), Description("Read the available automation catalog. Does not disclose repository credentials.")]
    public Task<string> Catalog(CancellationToken cancellationToken) => Send("automations", HttpMethod.Get, "/api/catalog", null, cancellationToken);
    [McpServerTool(Name = "get_automation_run", ReadOnly = true), Description("Read one execution belonging to the selected workspace.")]
    public Task<string> Run(Guid runId, CancellationToken cancellationToken) => Send("automations", HttpMethod.Get, $"/api/runs/{runId:D}", null, cancellationToken);
    [McpServerTool(Name = "get_automation_installation", ReadOnly = true), Description("Read the status or failure of one queued catalog installation in the selected workspace.")]
    public Task<string> Installation(Guid installationId, CancellationToken cancellationToken) => Send("automations", HttpMethod.Get, $"/api/installations/{installationId:D}", null, cancellationToken);
    [McpServerTool(Name = "install_automation", ReadOnly = false, Destructive = false), Description("Install a catalog asset in the selected workspace after explicit review. The automations API requires workspace ownership.")]
    public Task<string> Install(string assetId, CancellationToken cancellationToken) => Send("automations", HttpMethod.Post, "/api/catalog/install", new JsonObject { ["assetId"] = assetId }, cancellationToken);
    [McpServerTool(Name = "start_automation_run", ReadOnly = false, Destructive = false), Description("Start an installed automation after explicit review of its inputs and effects. Values must match the package manifest. May cause external side effects.")]
    public Task<string> StartRun(string packageKey, JsonObject values, CancellationToken cancellationToken) => Send("automations", HttpMethod.Post, "/api/runs", new JsonObject { ["packageKey"] = packageKey, ["values"] = values.DeepClone() }, cancellationToken);
    [McpServerTool(Name = "cancel_automation_run", ReadOnly = false, Destructive = true), Description("Request cancellation of a workspace execution after explicit user review. Does not undo completed side effects.")]
    public Task<string> CancelRun(Guid runId, CancellationToken cancellationToken) => Send("automations", HttpMethod.Post, $"/api/runs/{runId:D}/cancel", new JsonObject(), cancellationToken);

    [McpServerTool(Name = "get_ai_settings", ReadOnly = true), Description("Read AI configuration and usage for the selected workspace. Never returns OAuth tokens.")]
    public Task<string> AiSettings(CancellationToken cancellationToken) => Send("ai", HttpMethod.Get, "/api/workspace", null, cancellationToken);
    [McpServerTool(Name = "get_my_ai_connection", ReadOnly = true), Description("Read the current member's AI connection status and selected model. Never returns OAuth tokens.")]
    public Task<string> AiConnection(CancellationToken cancellationToken) => Send("ai", HttpMethod.Get, "/api/connection", null, cancellationToken);
    [McpServerTool(Name = "set_workspace_ai_enabled", ReadOnly = false, Destructive = false), Description("Enable or disable AI in the selected workspace after explicit review. The AI API requires workspace ownership. No monthly limit is introduced.")]
    public Task<string> SetAiEnabled(bool enabled, CancellationToken cancellationToken) => Send("ai", HttpMethod.Put, "/api/workspace", new JsonObject { ["enabled"] = enabled, ["provider"] = "openai" }, cancellationToken);
    [McpServerTool(Name = "set_my_ai_model", ReadOnly = false, Destructive = false), Description("Select a model for the current member's existing subscription connection after explicit review. The AI API validates model availability.")]
    public Task<string> SetModel(string model, CancellationToken cancellationToken) => Send("ai", HttpMethod.Put, "/api/connection/model", new JsonObject { ["model"] = model }, cancellationToken);
}
