using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Domus.Ai;

public sealed record OAuthAttempt(Guid Actor, Guid Workspace, string State, string Nonce, string Verifier, string Redirect, string ClientId, Guid Revision, DateTimeOffset ExpiresAt);
public sealed partial class ChatGptOAuth(AiStore store, IConfiguration configuration, HttpClient client)
{
    private const string Issuer = "https://auth.openai.com";
    private const string RequiredScope = "chatgpt.tokens.use.direct";
    private readonly ConcurrentDictionary<string, OAuthAttempt> attempts = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();
    private bool OpenSource => configuration["AI_OPENAI_OAUTH_MODE"] == "opensource";
    public bool Configured => OpenSource ? Uri.TryCreate(Origin, UriKind.Absolute, out var uri) && uri.Scheme == "http" && uri.Host == "127.0.0.1" : !string.IsNullOrWhiteSpace(configuration["AI_OPENAI_CLIENT_ID"]);
    private string Origin => (configuration["PORTAL_PUBLIC_URL"] ?? "http://localhost:8088").TrimEnd('/');
    private static string Random() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    private async Task<JsonObject> Send(HttpRequestMessage request, CancellationToken cancellation)
    {
        using (request)
        using (var response = await client.SendAsync(request, cancellation))
        {
            if (!response.IsSuccessStatusCode) throw new AiError(409, "Não foi possível autorizar a utilização do plano ChatGPT. Volte a ligar a conta.");
            return await response.Content.ReadFromJsonAsync<JsonObject>(cancellation) ?? throw new AiError(502, "Resposta OAuth inválida.");
        }
    }
    private async Task<JsonObject> Discovery(CancellationToken cancellation)
    {
        var metadata = await Send(new(HttpMethod.Get, Issuer + "/.well-known/openid-configuration"), cancellation);
        if (metadata["issuer"]?.GetValue<string>() != Issuer) throw new AiError(502, "Emissor OAuth inválido.");
        foreach (var key in new[] { "authorization_endpoint", "token_endpoint", "jwks_uri" })
        {
            if (!Uri.TryCreate(metadata[key]?.GetValue<string>(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "auth.openai.com") throw new AiError(502, "Endpoint OAuth inválido.");
        }
        return metadata;
    }
    public async Task<string> Begin(Identity actor, HttpContext context)
    {
        if (!Configured) throw new AiError(409, "O Domus precisa de um cliente OAuth autorizado para utilizar planos ChatGPT. A configuração do servidor ainda está pendente.");
        var metadata = await Discovery(context.RequestAborted);
        var (connection, revision) = await store.Connection(actor.UserId);
        foreach (var previous in attempts.Where(pair => pair.Value.Actor == actor.UserId || pair.Value.ExpiresAt < DateTimeOffset.UtcNow)) attempts.TryRemove(previous.Key, out _);
        var attempt = new OAuthAttempt(actor.UserId, actor.WorkspaceId, Random(), Random(), Random(), Origin + (OpenSource ? "/auth/callback" : "/ai-api/oauth/callback"), OpenSource ? connection?.ClientId ?? "dynamic_agent_client" : configuration["AI_OPENAI_CLIENT_ID"]!, revision, DateTimeOffset.UtcNow.AddMinutes(10));
        attempts[attempt.State] = attempt;
        context.Response.Cookies.Append("domus-ai-oauth", attempt.State, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = new Uri(Origin).Scheme == "https", Path = new Uri(attempt.Redirect).AbsolutePath, MaxAge = TimeSpan.FromMinutes(10) });
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = attempt.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = attempt.Redirect,
            ["scope"] = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct",
            ["resource"] = "https://api.openai.com/v1",
            ["state"] = attempt.State,
            ["nonce"] = attempt.Nonce,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(attempt.Verifier)))
        };
        if (OpenSource)
        {
            parameters["ext_agent_host_id"] = await store.HostId();
            if (attempt.ClientId == "dynamic_agent_client") parameters["agent_name_hint"] = "Domus";
            else if (connection is not null) { parameters["id_token_hint"] = connection.IdToken; parameters["login_hint"] = connection.Email; }
        }
        return QueryHelpers.AddQueryString(metadata["authorization_endpoint"]!.GetValue<string>(), parameters);
    }
    public async Task Callback(HttpContext context, DomusIdentity identity)
    {
        var state = context.Request.Query["state"].ToString();
        if (!attempts.TryGetValue(state, out var pending) || pending.ExpiresAt < DateTimeOffset.UtcNow || context.Request.Cookies["domus-ai-oauth"] != state) throw new AiError(400, "A ligação OAuth expirou ou não corresponde a este browser.");
        context.Request.Headers["X-Domus-Workspace"] = pending.Workspace.ToString();
        var actor = await identity.Authenticate(context, false);
        if (actor.UserId != pending.Actor || !attempts.TryRemove(state, out var attempt)) throw new AiError(403, "A sessão Domus mudou durante a ligação.");
        context.Response.Cookies.Delete("domus-ai-oauth", new CookieOptions { Path = new Uri(attempt.Redirect).AbsolutePath });
        if (context.Request.Query.ContainsKey("error")) throw new AiError(409, "A autorização ChatGPT foi cancelada ou recusada.");
        var code = context.Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code)) throw new AiError(400, "Código OAuth em falta.");
        var suppliedClient = context.Request.Query["client_id"].ToString();
        var issuedClient = attempt.ClientId == "dynamic_agent_client" ? suppliedClient : attempt.ClientId;
        if (issuedClient == "dynamic_agent_client" || !issuedClient.StartsWith("oaiapp_", StringComparison.Ordinal) || issuedClient.Length > 120 || (attempt.ClientId != "dynamic_agent_client" && suppliedClient.Length > 0 && suppliedClient != attempt.ClientId)) throw new AiError(400, "O registo OAuth não devolveu um cliente válido.");
        var metadata = await Discovery(context.RequestAborted);
        var tokens = await Exchange(metadata, new() { ["grant_type"] = "authorization_code", ["code"] = code, ["client_id"] = issuedClient, ["redirect_uri"] = attempt.Redirect, ["code_verifier"] = attempt.Verifier, ["resource"] = "https://api.openai.com/v1" }, context.RequestAborted);
        var idToken = tokens["id_token"]?.GetValue<string>() ?? throw new AiError(409, "O provedor não devolveu uma identidade OAuth.");
        var keys = await Send(new(HttpMethod.Get, metadata["jwks_uri"]!.GetValue<string>()), context.RequestAborted);
        var claims = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = issuedClient,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = new JsonWebKeySet(keys.ToJsonString()).GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        });
        if (!claims.IsValid || !claims.Claims.TryGetValue("nonce", out var nonce) || nonce.ToString() != attempt.Nonce || !claims.Claims.TryGetValue("sub", out var subject) || string.IsNullOrWhiteSpace(subject.ToString())) throw new AiError(409, "A identidade ChatGPT não pôde ser validada.");
        var scopes = Scopes(tokens);
        var (previous, _) = await store.Connection(actor.UserId);
        if (previous is not null && previous.Subject != subject.ToString()) throw new AiError(409, "A conta ChatGPT mudou. Desligue a conta anterior antes de ligar outra.");
        var connection = new ChatGptConnection(issuedClient, subject.ToString()!, (claims.Claims.TryGetValue("email", out var email) ? email?.ToString() ?? "" : ""), AccessToken(tokens), tokens["refresh_token"]?.GetValue<string>() ?? "", idToken, Expiry(tokens), scopes, previous?.Model ?? "");
        await store.SaveConnection(actor.UserId, connection, attempt.Revision);
    }
    private async Task<JsonObject> Exchange(JsonObject metadata, Dictionary<string, string> values, CancellationToken cancellation)
    {
        var secret = configuration["AI_OPENAI_CLIENT_SECRET"];
        if (!OpenSource && values.GetValueOrDefault("client_id") == configuration["AI_OPENAI_CLIENT_ID"] && !string.IsNullOrEmpty(secret)) values["client_secret"] = secret;
        return await Send(new(HttpMethod.Post, metadata["token_endpoint"]!.GetValue<string>()) { Content = new FormUrlEncodedContent(values) }, cancellation);
    }
    private static string AccessToken(JsonObject tokens)
    {
        if (!string.Equals(tokens["token_type"]?.GetValue<string>(), "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(tokens["access_token"]?.GetValue<string>())) throw new AiError(409, "Credencial OAuth inválida.");
        return tokens["access_token"]!.GetValue<string>();
    }
    private static DateTimeOffset Expiry(JsonObject tokens)
    {
        var seconds = tokens["expires_in"]?.GetValue<int>() ?? 0;
        if (seconds <= 0) throw new AiError(409, "Prazo OAuth inválido.");
        return DateTimeOffset.UtcNow.AddSeconds(seconds);
    }
    private static string[] Scopes(JsonObject tokens)
    {
        var scopes = (tokens["scope"]?.GetValue<string>() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!scopes.Contains(RequiredScope) || !scopes.Contains("resource.invoke")) throw new AiError(409, "Esta autorização não permite utilizar o plano ChatGPT para IA.");
        return scopes;
    }
    public async Task<ChatGptConnection> Credential(Guid actor, CancellationToken cancellation)
    {
        var gate = locks.GetOrAdd(actor, _ => new(1)); await gate.WaitAsync(cancellation);
        try
        {
            var (connection, revision) = await store.Connection(actor);
            if (connection is null) throw new AiError(409, "Ligue a sua conta ChatGPT nas definições de IA.");
            if (connection.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return connection;
            if (string.IsNullOrEmpty(connection.RefreshToken)) throw new AiError(409, "A ligação ChatGPT expirou. Volte a ligar a conta.");
            var metadata = await Discovery(cancellation);
            var tokens = await Exchange(metadata, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = connection.RefreshToken, ["client_id"] = connection.ClientId, ["resource"] = "https://api.openai.com/v1" }, cancellation);
            var updated = connection with { AccessToken = AccessToken(tokens), RefreshToken = tokens["refresh_token"]?.GetValue<string>() ?? connection.RefreshToken, IdToken = tokens["id_token"]?.GetValue<string>() ?? connection.IdToken, ExpiresAt = Expiry(tokens), Scopes = Scopes(tokens) };
            await store.SaveConnection(actor, updated, revision); return updated;
        }
        finally { gate.Release(); }
    }
    public async Task<AiModel[]> Models(Guid actor, CancellationToken cancellation)
    {
        var credential = await Credential(actor, cancellation);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new("Bearer", credential.AccessToken);
        var body = await Send(request, cancellation);
        return body["models"]?.AsArray().Where(m => m?["visibility"]?.GetValue<string>() == "list").Select(m => new AiModel(m!["slug"]!.GetValue<string>(), m["display_name"]!.GetValue<string>())).ToArray() ?? throw new AiError(502, "Catálogo de modelos ChatGPT inválido.");
    }
    public async Task Disconnect(Guid actor)
    {
        foreach (var pending in attempts.Where(pair => pair.Value.Actor == actor)) attempts.TryRemove(pending.Key, out _);
        var (_, revision) = await store.Connection(actor);
        await store.SaveConnection(actor, null, revision);
    }
}
