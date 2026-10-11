using Domus.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Nodes;
public static class McpClientChecks
{
    public static async Task Run(string url, Guid workspace)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false });
        var context = new DefaultHttpContext();context.Request.Headers.Cookie="owner";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["DOMUS_MCP_URL"]=url}).Build();
        await using var mcp=await DomusMcpClient.Connect(context,new(Guid.NewGuid(),workspace,false,false),"one",config,http,default);
        var tools=await mcp.Tools(default);
        if(tools[0]?["type"]?.GetValue<string>()!="namespace"||tools[0]?["tools"]?.AsArray().Count!=10)throw new Exception("Invalid function namespace from MCP discovery.");
        if (tools[0]!["tools"]!.AsArray().Any(t => !AssistantReadTools.Names.Contains(t!["name"]!.GetValue<string>()))) throw new Exception("Write tool leaked to assistant.");
        try { await mcp.Call("create_card", new JsonObject(), default); throw new Exception("Assistant accepted a write tool."); }
        catch (AiError) { }
        var search=await mcp.Call("search_cards",JsonNode.Parse("""{"query":"steering Sonae","status":null,"priority":null,"completed":null,"overdue":null,"assignee":null,"cursor":null,"limit":20}""")!.AsObject(),default);
        if(search["cards"]?[0]?["id"]?.GetValue<string>()!="card-999")throw new Exception("MCP SDK client did not retrieve scoped cards.");
        var detail=await mcp.Call("get_card",new JsonObject{["cardId"]="card-999"},default);
        if(detail["checklistDone"]?.GetValue<int>()!=3)throw new Exception("MCP SDK detail mismatch.");
        await using var automation = await DomusMcpClient.Connect(context, new(Guid.NewGuid(),workspace,false,false), "one", config, http, default, true);
        var automationTools = await automation.Tools(default);
        if (automationTools[0]!["tools"]!.AsArray().Count != 4) throw new Exception("Invalid automation tool discovery.");
        await automation.Call("get_automation_status", new JsonObject(), default);
        try { await automation.Call("search_cards", new JsonObject(), default); throw new Exception("Automation scope accepted board tools."); }
        catch (AiError) { }
        Console.WriteLine("PASS: AI MCP SDK client connects, discovers namespace tools and retrieves scoped card details.");
    }
}
