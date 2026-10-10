using Microsoft.AspNetCore.DataProtection;

namespace Automations.Api;

public sealed class GitHubTokenProtector(IDataProtectionProvider protection)
{
    private IDataProtector ForUser(Guid user) => protection.CreateProtector("Robolex.GitHub.PAT.v1", user.ToString());
    private IDataProtector ForPlatform() => protection.CreateProtector("Domus.GitHub.PAT.platform.v1");
    public string ProtectPlatform(string token) => ForPlatform().Protect(token);
    public string UnprotectPlatform(string encrypted) => ForPlatform().Unprotect(encrypted);
    public string Protect(Guid user, string token) => ForUser(user).Protect(token);
    public string Unprotect(Guid user, string encrypted) => ForUser(user).Unprotect(encrypted);
}
