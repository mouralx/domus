using Automations.Core;
using Microsoft.AspNetCore.DataProtection;
using System.Text.RegularExpressions;

namespace Automations.Api;

public sealed class GitHubCredentials(Database database, GitHubTokenProtector protection)
{
    public static bool PlatformMode => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOMUS_WORK_API_URL"));
    public async Task<string?> Repository(Guid scope) => PlatformMode ? (await PlatformGitHub.Read(database))?.Repository : await database.GitHubRepository(scope);
    public async Task MigratePlatform(ILogger logger)
    {
        if (!PlatformMode || await PlatformGitHub.Read(database) is not null) return;
        var legacy = await PlatformGitHub.Legacy(database);
        if (legacy.Count == 0) { await PlatformGitHub.Migrate(database, null, null); return; }
        var repositories = legacy.Select(x => x.Repository).Where(x => x is not null).Distinct(StringComparer.Ordinal).ToArray();
        var tokens = legacy.Where(x => x.EncryptedToken is not null).Select(x => protection.Unprotect(x.Scope, x.EncryptedToken!)).Distinct(StringComparer.Ordinal).ToArray();
        if (repositories.Length > 1 || tokens.Length > 1)
        {
            logger.LogWarning("Legacy GitHub settings conflict. A platform administrator must configure the platform repository and PAT.");
            await PlatformGitHub.Migrate(database, null, null); return;
        }
        await PlatformGitHub.Migrate(database, repositories.SingleOrDefault(), tokens.Length == 1 ? protection.ProtectPlatform(tokens[0]) : null);
    }
    public async Task<string?> Token(Guid user)
    {
        if (PlatformMode)
        {
            var encryptedPlatform = (await PlatformGitHub.Read(database))?.EncryptedToken;
            return encryptedPlatform is null ? Environment.GetEnvironmentVariable("GITHUB_TOKEN") : protection.UnprotectPlatform(encryptedPlatform);
        }
        var encrypted = await database.GitHubToken(user);
        return encrypted is null ? Environment.GetEnvironmentVariable("GITHUB_TOKEN") : protection.Unprotect(user, encrypted);
    }
    public async Task Save(Guid user, string? token, string? repository)
    {
        var normalized = repository is null ? null : CatalogEntries.NormalizeRepository(repository);
        string? encrypted = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            token = token.Trim();
            if (!Regex.IsMatch(token, "^[A-Za-z0-9_]{20,255}$")) throw new AppError(400, "Introduza um Personal Access Token GitHub válido.");
            encrypted = PlatformMode ? protection.ProtectPlatform(token) : protection.Protect(user, token);
        }
        if (normalized is null && encrypted is null) throw new AppError(400, "Configure o repositório ou introduza um token.");
        if (PlatformMode) await PlatformGitHub.Save(database, normalized, encrypted);
        else await database.SaveGitHubSettings(user, normalized, encrypted);
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/settings/github", async (HttpContext context, Database database) =>
        {
            if (PlatformMode)
            {
                var config = await PlatformGitHub.Read(database);
                return Results.Ok(new { saved = config?.EncryptedToken is not null, repository = config?.Repository, configured = config?.EncryptedToken is not null || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")), scope = "platform" });
            }
            var saved = await database.GitHubToken((Guid)context.Items["userId"]!) is not null;
            return Results.Ok(new { saved, repository = await database.GitHubRepository((Guid)context.Items["userId"]!), configured = saved || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")) });
        });
        app.MapPost("/api/settings/github", async (TokenInput input, HttpContext context, GitHubCredentials credentials) =>
        {
            await credentials.Save((Guid)context.Items["userId"]!, input.Token, input.Repository); return Results.Ok(new { ok = true });
        });
        app.MapPost("/api/settings/github/remove", async (HttpContext context, Database database) =>
        {
            if (PlatformMode) await PlatformGitHub.RemoveToken(database);
            else await database.RemoveGitHubToken((Guid)context.Items["userId"]!); return Results.Ok(new { ok = true });
        });
    }
    public sealed record TokenInput(string? Token, string? Repository = null);
}
