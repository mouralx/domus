using System.Text.Json.Nodes;
namespace Domus.Ai;

public interface IProjectTools { Task<JsonObject> Call(string name, JsonObject arguments, CancellationToken ct); }
public sealed record ResearchResult(Suggestion Suggestion, string[] SeenIds, int ToolCalls, long InputTokens, long OutputTokens);
public static class AssistantResearch
{
    public static async Task<ResearchResult> Run(OpenAiProvider provider, IProjectTools mcp, ChatGptConnection credential, string instructions, JsonArray payload, JsonArray tools, CancellationToken ct)
    {
        var seen = new HashSet<string>(); long inputTokens = 0, outputTokens = 0; var toolCalls = 0;
        ModelStep? final = null;
        for (var round = 0; round < 6; round++)
        {
            if (payload.ToJsonString().Length > 120000) throw new AiError(502, "A pesquisa atingiu o limite de contexto. Faça uma pergunta mais específica.");
            var step = await provider.Step(credential.Model, credential.AccessToken, instructions, payload, round == 5 || toolCalls >= 8 ? null : tools, ct);
            inputTokens += step.InputTokens; outputTokens += step.OutputTokens;
            var calls = step.Output.Where(node => node?["type"]?.GetValue<string>() == "function_call").ToArray();
            if (calls.Length == 0) { final = step; break; }
            if (round == 5 || toolCalls + calls.Length > 8) throw new AiError(502, "A pesquisa atingiu o limite de consultas. Faça uma pergunta mais específica.");
            // Include reasoning items as well as calls on stateless Responses continuation.
            foreach (var item in step.Output) payload.Add(item?.DeepClone());
            foreach (var call in calls)
            {
                var name = call!["name"]!.GetValue<string>();
                var normalized = name.StartsWith("domus.", StringComparison.Ordinal) ? name[6..] : name;
                if (!AssistantReadTools.Names.Contains(normalized)) throw new AiError(502, "O modelo pediu uma ferramenta não autorizada.");
                var raw = call["arguments"]!.GetValue<string>();
                if (raw.Length > 4096) throw new AiError(502, "Argumentos de ferramenta demasiado grandes.");
                JsonObject arguments;
                try { arguments = JsonNode.Parse(raw)?.AsObject() ?? throw new AiError(502, "Argumentos de ferramenta inválidos."); }
                catch (System.Text.Json.JsonException) { throw new AiError(502, "Argumentos de ferramenta inválidos."); }
                var result = await mcp.Call(normalized, arguments, ct); toolCalls++;
                if (result["cards"] is JsonArray cards) foreach (var card in cards) if (card?["id"] is { } cardId) seen.Add(cardId.GetValue<string>());
                if (result["id"] is { } detailId && result["title"] is not null) seen.Add(detailId.GetValue<string>());
                payload.Add(new JsonObject { ["type"] = "function_call_output", ["call_id"] = call["call_id"]!.GetValue<string>(), ["output"] = result.ToJsonString() });
            }
        }
        if (final is null) throw new AiError(502, "O assistente não concluiu a pesquisa.");
        var suggestion = OpenAiProvider.ParseSuggestion(final.Text);
        if (suggestion.Items.Any(id => !seen.Contains(id))) throw new AiError(502, "A resposta incluiu referências que não foram consultadas. Tente novamente.");
        return new(suggestion, seen.ToArray(), toolCalls, inputTokens, outputTokens);
    }
}
