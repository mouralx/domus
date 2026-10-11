using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Domus.Ai;

public sealed record ProtectedTransfer(int Version, string Nonce, string Ciphertext, string Tag);
public sealed record TransferExport(string Destination);
public sealed record TransferImport(ProtectedTransfer File, string Code);
public sealed record TransferPayload(Guid Id, string Destination, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, ChatGptConnection Connection);

public static class ConnectionTransfer
{
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("Domus.ChatGPT.Transfer.v1");
    public static string Destination(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/" || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.Host == "127.0.0.1"))) throw new AiError(400, "Indique o endereço HTTPS da instalação de destino.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    public static (ProtectedTransfer File, string Code) Protect(TransferPayload payload)
    {
        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (plaintext.Length > 20000) throw new AiError(400, "A ligação é demasiado grande para transferir.");
        var nonce = RandomNumberGenerator.GetBytes(12); var encrypted = new byte[plaintext.Length]; var tag = new byte[16];
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        try { using var cipher = new AesGcm(key, 16); cipher.Encrypt(nonce, plaintext, encrypted, tag, Purpose); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
        return (new(1, Convert.ToBase64String(nonce), Convert.ToBase64String(encrypted), Convert.ToBase64String(tag)), code);
    }
    public static TransferPayload Unprotect(TransferImport input, string destination)
    {
        try
        {
            if (input.File is null || input.File.Version != 1 || input.Code is null || input.Code.Length != 43 || input.File.Ciphertext is null || input.File.Ciphertext.Length > 27000) throw new CryptographicException();
            var nonce = Convert.FromBase64String(input.File.Nonce); var bytes = Convert.FromBase64String(input.File.Ciphertext); var tag = Convert.FromBase64String(input.File.Tag);
            if (nonce.Length != 12 || tag.Length != 16) throw new CryptographicException();
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(input.Code)); var plaintext = new byte[bytes.Length];
            TransferPayload payload;
            try { using var cipher = new AesGcm(key, 16); cipher.Decrypt(nonce, bytes, tag, plaintext, Purpose); payload = JsonSerializer.Deserialize<TransferPayload>(plaintext) ?? throw new CryptographicException(); }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
            if (payload.Id == Guid.Empty || payload.Connection is null || payload.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(1) || payload.ExpiresAt <= DateTimeOffset.UtcNow || payload.ExpiresAt > payload.CreatedAt.AddMinutes(30)) throw new AiError(409, "A transferência expirou. Exporte uma nova ligação.");
            if (payload.Destination != Destination(destination)) throw new AiError(403, "Este ficheiro pertence a outra instalação Domus.");
            return payload;
        }
        catch (Exception error) when (error is CryptographicException or FormatException or JsonException or ArgumentException) { throw new AiError(400, "Ficheiro de ligação ou código de transferência inválido."); }
    }
}

public sealed partial class ChatGptOAuth
{
    private readonly SemaphoreSlim importGate = new(1);
    public async Task<object> Export(Guid actor, string destination, CancellationToken ct)
    {
        if (!OpenSource && !string.IsNullOrWhiteSpace(configuration["AI_OPENAI_CLIENT_SECRET"])) throw new AiError(409, "A transferência requer uma ligação OAuth pública de código aberto.");
        destination = ConnectionTransfer.Destination(destination);
        if (destination == ConnectionTransfer.Destination(Origin)) throw new AiError(400, "Escolha uma instalação de destino diferente.");
        var gate = locks.GetOrAdd(actor, _ => new(1)); await gate.WaitAsync(ct);
        try
        {
            var (connection, revision) = await store.Connection(actor);
            if (connection is null || string.IsNullOrWhiteSpace(connection.RefreshToken)) throw new AiError(409, "Ligue primeiro a sua conta ChatGPT com autorização de utilização do plano.");
            var now = DateTimeOffset.UtcNow;
            var transfer = ConnectionTransfer.Protect(new(Guid.NewGuid(), destination, now, now.AddMinutes(30), connection));
            foreach (var pending in attempts.Where(p => p.Value.Actor == actor)) attempts.TryRemove(pending.Key, out _);
            // Move ownership: the local installation must no longer rotate these tokens.
            await store.SaveConnection(actor, null, revision);
            return new { file = transfer.File, code = transfer.Code, email = connection.Email };
        }
        finally { gate.Release(); }
    }
    public async Task Import(Guid actor, TransferImport input, CancellationToken ct)
    {
        var payload = ConnectionTransfer.Unprotect(input, Origin);
        await importGate.WaitAsync(ct);
        var gate = locks.GetOrAdd(actor, _ => new(1));
        try
        {
            await gate.WaitAsync(ct);
            try
            {
                var (existing, revision) = await store.Connection(actor);
                if (existing is not null) throw new AiError(409, "Desligue a ligação atual antes de importar outra.");
                var key = "transfer:" + payload.Id.ToString("N");
                if (await store.TransferUsed(key)) throw new AiError(409, "Esta transferência já foi utilizada.");
                var credential = payload.Connection;
                if (string.IsNullOrWhiteSpace(credential.ClientId) || credential.ClientId.Length > 120 || !credential.ClientId.StartsWith("oaiapp_", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(credential.Subject) || credential.Subject.Length > 512 || string.IsNullOrWhiteSpace(credential.IdToken) || string.IsNullOrWhiteSpace(credential.RefreshToken)) throw new AiError(400, "Registo OAuth inválido.");
                var metadata = await Discovery(ct);
                var tokens = await Exchange(metadata, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = credential.RefreshToken, ["client_id"] = credential.ClientId, ["resource"] = "https://api.openai.com/v1" }, ct);
                var refreshed = credential with { AccessToken = AccessToken(tokens), RefreshToken = tokens["refresh_token"]?.GetValue<string>() ?? credential.RefreshToken, IdToken = tokens["id_token"]?.GetValue<string>() ?? credential.IdToken, ExpiresAt = Expiry(tokens), Scopes = Scopes(tokens), Model = credential.Model ?? "" };
                var keys = await Send(new(HttpMethod.Get, metadata["jwks_uri"]!.GetValue<string>()), ct);
                var claims = await new JsonWebTokenHandler().ValidateTokenAsync(refreshed.IdToken, new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = refreshed.ClientId,
                    // Retained identity tokens may be expired; the live refresh grant above proves current authorization.
                    ValidateLifetime = false, RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = new JsonWebKeySet(keys.ToJsonString()).GetSigningKeys(), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                });
                if (!claims.IsValid || !claims.Claims.TryGetValue("sub", out var subject) || subject.ToString() != credential.Subject) throw new AiError(409, "A identidade ChatGPT não pôde ser validada.");
                var email = claims.Claims.TryGetValue("email", out var value) ? value?.ToString() ?? "" : "";
                await store.HostId(); // Never import or overwrite the source installation's host ID.
                await store.SaveConnection(actor, refreshed with { Email = email }, revision, key);
            }
            finally { gate.Release(); }
        }
        finally { importGate.Release(); }
    }
}
