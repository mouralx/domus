using System.Text.Json;
using System.Text.Json.Nodes;

namespace Domus.Ai;

public sealed record ModelStep(JsonArray Output, string Text, long InputTokens, long OutputTokens);
public sealed class OpenAiProvider(HttpClient client)
{
    private static readonly object Schema = new
    {
        type = "object",
        additionalProperties = false,
        properties = new { text = new { type = "string" }, items = new { type = "array", items = new { type = "string" } } },
        required = new[] { "text", "items" }
    };
    public async Task<Generation> Generate(string model, string accessToken, string instructions, string context, CancellationToken cancellation)
    {
        var step = await Step(model, accessToken, instructions, new JsonArray(new JsonObject { ["role"] = "user", ["content"] = context }), null, cancellation);
        return new(ParseSuggestion(step.Text), step.InputTokens, step.OutputTokens);
    }
    public async Task<ModelStep> Step(string model, string accessToken, string instructions, JsonArray input, JsonArray? tools, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new("Bearer", accessToken);
        var payload = new JsonObject
        {
            ["model"] = model,
            ["store"] = false,
            ["stream"] = true,
            ["instructions"] = instructions,
            ["input"] = input.DeepClone(),
            ["text"] = JsonSerializer.SerializeToNode(new { format = new { type = "json_schema", name = "domus_answer", strict = true, schema = Schema } })
        };
        if (tools is not null) payload["tools"] = tools.DeepClone();
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode) throw new AiError((int)response.StatusCode == 429 ? 429 : 502,
            (int)response.StatusCode == 429 ? "O plano ChatGPT atingiu o limite de utilização. Tente mais tarde." : "O ChatGPT recusou o pedido. Verifique a ligação e o modelo selecionado.");
        // Some compatible gateways return a completed JSON response even when streaming is requested.
        // Keep the streaming request contract, and validate completion for either transport.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType == "application/json")
        {
            await response.Content.LoadIntoBufferAsync(262144, timeout.Token);
            JsonNode? body;
            try { body = JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token)); }
            catch (JsonException) { throw new AiError(502, "O ChatGPT devolveu JSON inválido."); }
            var completed = body?["response"] ?? body;
            if (completed?["error"] is not null) throw new AiError(502, "O ChatGPT não conseguiu concluir o pedido.");
            if (completed?["status"]?.GetValue<string>() != "completed") throw new AiError(502, "A resposta de IA ficou incompleta. Tente novamente.");
            return ParseCompleted(completed);
        }
        if (mediaType is not ("text/event-stream" or "application/octet-stream" or null)) throw new AiError(502, "O ChatGPT devolveu um formato de resposta inesperado.");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        var data = new List<string>(); int bytes = 0;
        var streamedText = new System.Text.StringBuilder();
        var streamedItems = new SortedDictionary<int, JsonNode>();
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            bytes += line.Length;
            if (bytes > 262144) throw new AiError(502, "Resposta de IA demasiado grande.");
            if (line.StartsWith("data:", StringComparison.Ordinal)) data.Add(line[5..].TrimStart());
            if (line.Length != 0 || data.Count == 0) continue;
            var raw = string.Join('\n', data); data.Clear();
            if (raw == "[DONE]") break;
            var item = JsonNode.Parse(raw) ?? throw new AiError(502, "Resposta ChatGPT inválida.");
            var type = item["type"]?.GetValue<string>();
            if (type is "response.failed" or "error")
            {
                var code = item["response"]?["error"]?["code"]?.GetValue<string>() ?? item["code"]?.GetValue<string>();
                throw new AiError(429, code == "subscription_sharing_usage_unavailable" ? "O seu plano não permite esta utilização. Verifique as permissões ChatGPT." : "O ChatGPT não conseguiu concluir o pedido. Verifique os limites do seu plano.");
            }
            if (type == "response.incomplete") throw new AiError(502, "A resposta de IA ficou incompleta. Tente novamente.");
            if (type == "response.output_text.delta")
            {
                streamedText.Append(item["delta"]?.GetValue<string>());
                if (streamedText.Length > 16000) throw new AiError(502, "Resposta de IA demasiado grande.");
            }
            if (type == "response.output_text.done" && streamedText.Length == 0)
            {
                streamedText.Append(item["text"]?.GetValue<string>());
                if (streamedText.Length > 16000) throw new AiError(502, "Resposta de IA demasiado grande.");
            }
            if (type == "response.output_item.done" && item["item"] is { } done)
                streamedItems[item["output_index"]?.GetValue<int>() ?? streamedItems.Count] = done.DeepClone();
            if (type != "response.completed") continue;
            var completed = item["response"] ?? throw new AiError(502, "Resposta ChatGPT inválida.");
            return ParseCompleted(completed, streamedText.ToString(), new JsonArray(streamedItems.Values.Select(x => x.DeepClone()).ToArray()));
        }
        throw new AiError(502, "A ligação ChatGPT terminou antes de concluir a sugestão.");
    }
    private static ModelStep ParseCompleted(JsonNode completed, string streamedText = "", JsonArray? streamedItems = null)
    {
        var output = completed["output"]?.AsArray();
        if (output is null || output.Count == 0) output = streamedItems ?? [];
        var text = string.Concat(output.SelectMany(x => x?["content"]?.AsArray() ?? []).Where(x => x?["type"]?.GetValue<string>() == "output_text").Select(x => x?["text"]?.GetValue<string>()) ?? []);
        if (string.IsNullOrWhiteSpace(text)) text = streamedText;
        if (text.Length > 16000) throw new AiError(502, "Resposta de IA demasiado grande.");
        return new((JsonArray)output.DeepClone(), text, completed["usage"]?["input_tokens"]?.GetValue<long>() ?? 0, completed["usage"]?["output_tokens"]?.GetValue<long>() ?? 0);
    }
    public static Suggestion ParseSuggestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 16000) throw new AiError(502, "O modelo não devolveu uma sugestão válida.");
        Suggestion? result;
        try { result = JsonSerializer.Deserialize<Suggestion>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { throw new AiError(502, "O modelo não devolveu uma sugestão válida."); }
        if (result is null || result.Text is null || result.Items is null || result.Items.Length > 30 || result.Items.Any(i => string.IsNullOrWhiteSpace(i) || i.Length > 500)) throw new AiError(502, "O modelo não devolveu uma sugestão válida.");
        return result;
    }
}
