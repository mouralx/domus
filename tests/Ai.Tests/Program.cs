using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domus.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task Rejected(Func<Task> action, int status)
{
    try { await action(); throw new Exception("Expected rejection: " + status); }
    catch (AiError error) { Assert(error.Status == status, "Wrong rejection status: " + error.Status); }
}
var directory = Path.Combine(Path.GetTempPath(), "domus-ai-test-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
try
{
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI_DATA_DIR"] = directory, ["AI_OPENAI_CLIENT_ID"] = "oaiapp_synthetic", ["PORTAL_PUBLIC_URL"] = "http://localhost:8088", ["DOMUS_WORK_API_URL"] = "http://boards.test" }).Build();
    var protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory, "keys")));
    var store = new AiStore(protection, configuration); var actor = Guid.NewGuid(); var workspace = Guid.NewGuid();
    var connection = new ChatGptConnection("oaiapp_synthetic", "subject", "test@example.test", "synthetic-access", "synthetic-refresh", "synthetic-id", DateTimeOffset.UtcNow.AddHours(1), ["resource.invoke", "chatgpt.tokens.use.direct"], "test-model");
    await store.SaveConnection(actor, connection, Guid.Empty);
    Assert(!File.ReadAllText(Path.Combine(directory, "settings.json")).Contains("synthetic-access"), "Token stored in plaintext");
    var restarted = new AiStore(protection, configuration);
    Assert((await restarted.Connection(actor)).Connection!.AccessToken == "synthetic-access", "Credential did not survive restart");
    Assert((await restarted.Connection(Guid.NewGuid())).Connection is null, "Credentials shared across users");
    await restarted.SaveWorkspace(workspace, new(true, "openai"));
    // Complete 110 requests across members: no monthly cap, even beyond the old default 100.
    for (var member = 0; member < 11; member++)
    {
        var id = Guid.NewGuid(); await restarted.SaveConnection(id, connection, Guid.Empty);
        for (var request = 0; request < 10; request++)
        {
            var record = await restarted.Reserve(new(id, workspace, true, false), "test-model", "summary");
            await restarted.Finish(record.Id, "completed", 1, 2);
        }
    }
    await Rejected(() => restarted.Reserve(new(actor, Guid.NewGuid(), true, false), "test-model", "summary"), 409);
    var (_, revision) = await restarted.Connection(actor);
    await restarted.SaveConnection(actor, null, revision);
    await Rejected(() => restarted.SaveConnection(actor, connection, revision), 409);
    Assert((await restarted.Connection(actor)).Connection is null, "Stale refresh restored disconnected account");
    Console.WriteLine("PASS: encrypted per-user tokens, restart, no monthly cap, isolation and disconnect race protection.");

    using var rsa = RSA.Create(2048); var signingKey = new RsaSecurityKey(rsa) { KeyId = "synthetic-key" };
    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "synthetic-key" });
    string expectedRedirect = "http://localhost:8088/ai-api/oauth/callback";
    string? nonce = null; bool wrongNonce = false; int exchanges = 0, refreshes = 0;
    var handler = new StubHandler(async request =>
    {
        var uri = request.RequestUri!;
        if (uri.Host == "boards.test") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = actor, workspaceId = workspace, canManage = true }) };
        if (uri.AbsolutePath == "/.well-known/openid-configuration") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { issuer = "https://auth.openai.com", authorization_endpoint = "https://auth.openai.com/api/accounts/authorize", token_endpoint = "https://auth.openai.com/api/accounts/oauth/token", jwks_uri = "https://auth.openai.com/.well-known/jwks.json" }) };
        if (uri.AbsolutePath == "/.well-known/jwks.json") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { keys = new[] { jwk } }) };
        if (uri.AbsolutePath == "/v1/models") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { models = new[] { new { slug = "test-model", display_name = "Test model", visibility = "list" } } }) };
        var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync());
        Assert(!form.ContainsKey("client_secret"), "Public OAuth unexpectedly used a secret");
        var scopes = "openid resource.invoke chatgpt.tokens.use.direct offline_access";
        if (form["grant_type"] == "refresh_token")
        {
            refreshes++; await Task.Delay(20);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "renewed-access", refresh_token = "rotated-refresh", token_type = "Bearer", expires_in = 3600, scope = scopes }) };
        }
        exchanges++; Assert(form["redirect_uri"] == expectedRedirect, "Callback changed during exchange");
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = "https://auth.openai.com", Audience = "oaiapp_synthetic", Expires = DateTime.UtcNow.AddMinutes(10), SigningCredentials = new(signingKey, SecurityAlgorithms.RsaSha256), Claims = new Dictionary<string, object> { ["sub"] = "subject", ["email"] = "test@example.test", ["nonce"] = wrongNonce ? "wrong" : nonce! } });
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "synthetic-access", refresh_token = "synthetic-refresh", id_token = token, token_type = "Bearer", expires_in = 3600, scope = scopes }) };
    });
    var http = new HttpClient(handler); var oauth = new ChatGptOAuth(restarted, configuration, http); var identity = new DomusIdentity(configuration, http);
    var context = new DefaultHttpContext(); context.Response.Body = new MemoryStream();
    var url = new Uri(await oauth.Begin(new(actor, workspace, true, false), context));
    var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Query);
    nonce = parameters["nonce"];
    Assert(parameters["code_challenge_method"] == "S256" && parameters["code_challenge"].ToString().Length >= 40, "PKCE missing");
    DefaultHttpContext Callback(string state)
    {
        var callback = new DefaultHttpContext(); callback.Response.Body = new MemoryStream();
        callback.Request.QueryString = new("?code=synthetic-code&state=" + state);
        callback.Request.Headers.Cookie = "domus-ai-oauth=" + state;
        return callback;
    }
    await Rejected(() => oauth.Callback(Callback("wrong-state"), identity), 400);
    await oauth.Callback(Callback(parameters["state"]!), identity);
    await Rejected(() => oauth.Callback(Callback(parameters["state"]!), identity), 400);
    Assert(exchanges == 1, "Code replay triggered another exchange");
    Assert((await restarted.Connection(actor)).Connection!.Subject == "subject", "Validated identity missing");
    Assert((await oauth.Models(actor, default)).Single().Id == "test-model", "Account-specific catalog missing");
    var (saved, savedRevision) = await restarted.Connection(actor);
    await restarted.SaveConnection(actor, saved! with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }, savedRevision);
    await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => oauth.Credential(actor, default)));
    Assert(refreshes == 1 && (await restarted.Connection(actor)).Connection!.RefreshToken == "rotated-refresh", "Concurrent refresh did not rotate atomically");
    var second = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(await oauth.Begin(new(actor, workspace, true, false), context)).Query);
    nonce = second["nonce"]; wrongNonce = true;
    await Rejected(() => oauth.Callback(Callback(second["state"]!), identity), 409);
    await oauth.Disconnect(actor);
    Assert((await restarted.Connection(actor)).Connection is null, "Disconnect failed");
    Console.WriteLine("PASS: PKCE, state/replay protection, signed ID token/nonce, account model catalog, serialized rotating refresh and disconnect.");
    configuration["AI_OPENAI_OAUTH_MODE"] = "opensource";
    configuration["PORTAL_PUBLIC_URL"] = "http://127.0.0.1:8088";
    configuration["AI_OPENAI_CLIENT_ID"] = "";
    expectedRedirect = "http://127.0.0.1:8088/auth/callback"; wrongNonce = false;
    var dynamicParameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(await oauth.Begin(new(actor, workspace, true, false), context)).Query);
    nonce = dynamicParameters["nonce"];
    Assert(dynamicParameters["client_id"] == "dynamic_agent_client" && dynamicParameters["agent_name_hint"] == "Domus", "OSS initial registration did not identify Domus");
    var hostId = await restarted.HostId(); Assert(dynamicParameters["ext_agent_host_id"] == hostId, "Host attribution missing");
    Assert(await new AiStore(protection, configuration).HostId() == hostId, "Host ID did not survive restart");
    var dynamicCallback = Callback(dynamicParameters["state"]!);
    dynamicCallback.Request.QueryString = dynamicCallback.Request.QueryString.Add(new("?client_id=oaiapp_synthetic"));
    await oauth.Callback(dynamicCallback, identity);
    Assert((await restarted.Connection(actor)).Connection!.ClientId == "oaiapp_synthetic", "Issued client ID was not retained");
    var returning = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(await oauth.Begin(new(actor, workspace, true, false), context)).Query);
    Assert(returning["client_id"] == "oaiapp_synthetic" && !returning.ContainsKey("agent_name_hint") && returning["ext_agent_host_id"] == hostId, "Returning registration identity changed");
    configuration["PORTAL_PUBLIC_URL"] = "https://domus.example";
    Assert(!oauth.Configured, "Remote web host must not claim loopback flow is ready");
    Console.WriteLine("PASS: OSS dynamic registration, issued client reuse, stable host ID and loopback-only readiness.");

    var sessions = new AssistantConversations();
    var (sessionId, session) = sessions.Open(new(actor, workspace, true, false), "one", null);
    Assert(sessions.Open(new(actor, workspace, true, false), "one", sessionId).Conversation == session, "Session lost");
    await Rejected(() => Task.FromResult(sessions.Open(new(Guid.NewGuid(), workspace, true, false), "one", sessionId)), 404);
    await Rejected(() => Task.FromResult(sessions.Open(new(actor, Guid.NewGuid(), true, false), "one", sessionId)), 404);
    await Rejected(() => Task.FromResult(sessions.Open(new(actor, workspace, true, false), "two", sessionId)), 404);
    session.LastUsed = DateTimeOffset.UtcNow.AddMinutes(-31);
    await Rejected(() => Task.FromResult(sessions.Open(new(actor, workspace, true, false), "one", sessionId)), 404);
    Console.WriteLine("PASS: scoped and expiring project conversations.");
    await ResearchChecks.Run();

    var suggestion = JsonSerializer.Serialize(new { text = "A useful summary", items = new[] { "Review the card" } });
    var responseBody = JsonSerializer.Serialize(new { type = "response.completed", response = new { output = new[] { new { content = new[] { new { type = "output_text", text = suggestion } } } }, usage = new { input_tokens = 10, output_tokens = 20 } } });
    var provider = new OpenAiProvider(new HttpClient(new StubHandler(async request =>
    {
        Assert(request.Headers.Authorization?.Parameter == "synthetic-access", "OAuth bearer missing");
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
        Assert(body["stream"]!.GetValue<bool>() && !body["store"]!.GetValue<bool>() && body["input"] is JsonArray, "Subscription request contract");
        Assert(body["max_output_tokens"] is null && body["temperature"] is null, "Unsupported subscription field");
        return new(HttpStatusCode.OK) { Content = new StringContent("data: " + responseBody + "\n\n", System.Text.Encoding.UTF8, "text/event-stream") };
    })));
    var generated = await provider.Generate("test-model", "synthetic-access", "instructions", "card", default);
    Assert(generated.Suggestion.Text == "A useful summary" && generated.InputTokens == 10, "SSE result not normalized");
    await Rejected(() => new OpenAiProvider(new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"type\":\"response.failed\",\"response\":{\"error\":{\"code\":\"subscription_sharing_usage_limit_exceeded\"}}}\n\n", System.Text.Encoding.UTF8, "text/event-stream") })))).Generate("test", "token", "instructions", "card", default), 429);
    var emptyCompleted = "data: {\"type\":\"response.completed\",\"response\":{\"output\":[],\"usage\":{\"input_tokens\":2,\"output_tokens\":3}}}\n\n";
    var deltaStream = "data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = suggestion }) + "\n\n";
    var streamedProvider = new OpenAiProvider(new HttpClient(new StubHandler(_ => { var content = new StringContent(deltaStream + emptyCompleted); content.Headers.ContentType = null; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }); })));
    var streamed = await streamedProvider.Generate("test", "token", "instructions", "context", default);
    Assert(streamed.Suggestion.Text == "A useful summary" && streamed.OutputTokens == 3, "Missing content type or empty terminal output discarded streamed text");
    await Rejected(() => new OpenAiProvider(new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(deltaStream, System.Text.Encoding.UTF8, "text/event-stream") })))).Generate("test", "token", "instructions", "context", default), 502);
    var jsonResponse = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { content = new[] { new { type = "output_text", text = suggestion } } } }, usage = new { input_tokens = 10, output_tokens = 20 } });
    var jsonProvider = new OpenAiProvider(new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json") }))));
    Assert((await jsonProvider.Generate("test", "token", "instructions", "context", default)).Suggestion.Text == "A useful summary", "Completed JSON transport rejected");
    var incompleteProvider = new OpenAiProvider(new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"in_progress\"}", System.Text.Encoding.UTF8, "application/json") }))));
    await Rejected(() => incompleteProvider.Generate("test", "token", "instructions", "context", default), 502);
    Console.WriteLine("PASS: subscription Responses SSE/JSON transport, validated completion and streamed usage-limit failure.");
}
finally { Directory.Delete(directory, true); }
if (Environment.GetEnvironmentVariable("AI_TEST_POSTGRES") is { Length: > 0 } sqlConnection) await SqlStoreChecks.Run(sqlConnection);
if (Environment.GetEnvironmentVariable("MCP_TEST_URL") is { Length: > 0 } mcpUrl) await McpClientChecks.Run(mcpUrl, Guid.Parse(Environment.GetEnvironmentVariable("MCP_TEST_WORKSPACE")!));
sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
}
