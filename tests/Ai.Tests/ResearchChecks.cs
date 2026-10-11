using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domus.Ai;
public static class ResearchChecks
{
    public static async Task Run()
    {
        var round = 0;
        var provider = new OpenAiProvider(new HttpClient(new ResearchHandler(async request => {
            var input = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
            if (input["previous_response_id"] is not null || input["store"]!.GetValue<bool>() || !input["stream"]!.GetValue<bool>()) throw new Exception("Invalid stateless subscription tool request.");
            if (input["tools"]?[0]?["type"]?.GetValue<string>() != "namespace") throw new Exception("Tools not namespaced.");
            if (round > 0 && !input["input"]!.AsArray().Any(item => item?["type"]?.GetValue<string>() == "function_call_output")) throw new Exception("Tool outputs missing from continuation.");
            if (round > 0 && !input["input"]!.AsArray().Any(item => item?["type"]?.GetValue<string>() == "reasoning")) throw new Exception("Reasoning state missing from continuation.");
            JsonNode item = round++ switch {
                0 => JsonSerializer.SerializeToNode(new {type="function_call",name="search_cards",call_id="search",arguments="{\"query\":\"steering Sonae\"}"})!,
                1 => JsonSerializer.SerializeToNode(new {type="function_call",name="get_card",call_id="detail",arguments="{\"cardId\":\"card-999\"}"})!,
                _ => JsonSerializer.SerializeToNode(new {type="message",content=new[]{new{type="output_text",text="{\"text\":\"The steering card has three completed steps.\",\"items\":[\"card-999\"]}"}}})!
            };
            var stream="data: " + JsonSerializer.Serialize(new{type="response.output_item.done",output_index=0,item=new{type="reasoning",encrypted_content="synthetic-reasoning"}})+"\n\n";
            stream += "data: "+JsonSerializer.Serialize(new{type="response.output_item.done",output_index=1,item})+"\n\n";
            stream += "data: {\"type\":\"response.completed\",\"response\":{\"output\":[],\"usage\":{\"input_tokens\":10,\"output_tokens\":20}}}\n\n";
            var content=new StringContent(stream);content.Headers.ContentType=null;
            return new HttpResponseMessage(HttpStatusCode.OK){Content=content};
        })));
        var credential=new ChatGptConnection("test","test","test","token","refresh","id",DateTimeOffset.UtcNow.AddHours(1),[],"model");
        var tools=JsonNode.Parse("""[{"type":"namespace","name":"domus","tools":[]}]""")!.AsArray();
        var result=await AssistantResearch.Run(provider,new FakeTools(),credential,"instructions",new JsonArray(new JsonObject{["role"]="user",["content"]="Find steering Sonae"}),tools,default);
        if(result.ToolCalls!=2||result.InputTokens!=30||result.OutputTokens!=60||result.SeenIds.Single()!="card-999"||result.Suggestion.Items.Single()!="card-999")throw new Exception("Research did not complete the search/detail/answer cycle.");
        var forbidden=new OpenAiProvider(new HttpClient(new ResearchHandler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("data: {\"type\":\"response.completed\",\"response\":{\"output\":[{\"type\":\"function_call\",\"name\":\"delete_card\",\"call_id\":\"bad\",\"arguments\":\"{}\"}]}}\n\n",System.Text.Encoding.UTF8,"text/event-stream")}))));
        try{await AssistantResearch.Run(forbidden,new FakeTools(),credential,"instructions",new JsonArray(),tools,default);throw new Exception("Mutation tool was accepted.");}catch(AiError error)when(error.Status==502){}
        Console.WriteLine("PASS: model→MCP search/detail→answer loop, namespaced tools, streamed calls, reasoning continuation, token accounting and forbidden mutation rejection.");
    }
    private sealed class FakeTools : IProjectTools
    {
        public Task<JsonObject> Call(string name,JsonObject arguments,CancellationToken ct)=>Task.FromResult(JsonNode.Parse(name=="search_cards"?"""{"cards":[{"id":"card-999","title":"Steering Sonae"}],"hasMore":false}""":"""{"id":"card-999","title":"Steering Sonae","checklistDone":3}""")!.AsObject());
    }
    private sealed class ResearchHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handler):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>handler(request);}
}
