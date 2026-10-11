using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Domus.Ai;

public sealed class DomusMcpClient(McpClient client, bool automationOnly = false, bool allowActions = false) : IAsyncDisposable, IProjectTools
{
    public static async Task<DomusMcpClient> Connect(HttpContext context, Identity actor, string project, IConfiguration configuration, HttpClient http, CancellationToken ct, bool automationOnly = false, bool allowActions = false)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(configuration["DOMUS_MCP_URL"] ?? "http://mcp-server:4340/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Cookie"] = context.Request.Headers.Cookie.ToString(), ["X-Domus-Workspace"] = actor.WorkspaceId.ToString(), ["X-Domus-Project"] = project }
        }, http);
        try { return new(await McpClient.CreateAsync(transport, cancellationToken: ct), automationOnly, allowActions); }
        catch { await transport.DisposeAsync(); throw new AiError(503, "Não foi possível ligar ao servidor MCP do Domus."); }
    }
    public async Task<JsonArray> Tools(CancellationToken ct)
    {
        var definitions = await client.ListToolsAsync(cancellationToken: ct);
        var allowed = allowActions ? AssistantActionTools.Names : automationOnly ? AssistantReadTools.AutomationNames : AssistantReadTools.Names;
        var functions = new JsonArray();
        foreach (var tool in definitions.Where(t => allowed.Contains(t.Name)))
        {
            var schema = JsonNode.Parse(tool.JsonSchema.GetRawText())!.AsObject();
            schema["additionalProperties"] = false;
            schema["required"] = new JsonArray(schema["properties"]!.AsObject().Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray());
            functions.Add(new JsonObject { ["type"] = "function", ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = schema, ["strict"] = !AssistantActionTools.Mutations.Contains(tool.Name) });
        }
        if (!(automationOnly ? AssistantReadTools.AutomationNames : new HashSet<string> { "search_cards", "get_card", "get_project_metrics" }).All(name => definitions.Any(t => t.Name == name))) throw new AiError(503, "As ferramentas MCP necessárias estão indisponíveis.");
        return new JsonArray(new JsonObject { ["type"] = "namespace", ["name"] = "domus", ["description"] = "Authenticated tools for Domus workspace data, automations, current member and AI configuration. Changes require user confirmation.", ["tools"] = functions });
    }
    public async Task<JsonObject> Call(string name, JsonObject arguments, CancellationToken ct)
    {
        if (name.StartsWith("domus.", StringComparison.Ordinal)) name = name[6..];
        if (!(allowActions ? AssistantActionTools.Names : automationOnly ? AssistantReadTools.AutomationNames : AssistantReadTools.Names).Contains(name)) throw new AiError(502, "O modelo pediu uma ferramenta não autorizada.");
        var values = arguments.ToDictionary(p => p.Key, p => p.Value is null ? (object?)null : JsonSerializer.Deserialize<JsonElement>(p.Value.ToJsonString()));
        var result = await client.CallToolAsync(name, values, cancellationToken: ct);
        if (result.IsError == true) throw new AiError(502, "Não foi possível consultar os dados do Domus. Reveja os filtros ou tente novamente.");
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        if (text.Length > 65536) throw new AiError(502, "A ferramenta devolveu demasiados dados.");
        return JsonNode.Parse(text)?.AsObject() ?? throw new AiError(502, "Resposta MCP inválida.");
    }
    public ValueTask DisposeAsync() => client.DisposeAsync();
}

public static class AssistantReadTools
{
    public static readonly HashSet<string> AutomationNames = new(StringComparer.Ordinal) { "get_automation_status", "get_automation_catalog", "read_automation_catalog", "get_automation_run", "get_automation_installation", "list_automation_data", "read_automation_data", "read_automation_result" };
    public static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "list_workspace_data", "search_cards", "get_card", "read_card_data", "read_card_attachment", "get_project_metrics", "get_my_profile",
        "get_automation_status", "get_automation_catalog", "read_automation_catalog", "get_automation_run", "get_automation_installation", "list_automation_data", "read_automation_data", "read_automation_result",
        "get_ai_settings", "get_my_ai_connection"
    };
}

public static class AssistantActionTools
{
    public static readonly HashSet<string> Mutations = new(StringComparer.Ordinal) { "create_card", "change_workspace_item", "update_my_profile", "install_automation", "start_automation_run", "cancel_automation_run", "set_workspace_ai_enabled", "set_my_ai_model" };
    public static readonly HashSet<string> Names = new(AssistantReadTools.Names.Concat(Mutations), StringComparer.Ordinal);
}
