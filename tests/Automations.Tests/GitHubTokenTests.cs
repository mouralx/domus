using Automations.Api;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using Xunit;

namespace Automations.Tests;

public sealed class GitHubTokenTests
{
    [Fact]
    public void SavedTokenSurvivesProviderRecreationAndCannotBeDecryptedByAnotherUser()
    {
        var directory = Directory.CreateTempSubdirectory("robolex-key-test-");
        try
        {
            var user = Guid.NewGuid();
            const string token = "ghp_synthetic_private_token_0123456789";
            GitHubTokenProtector Create() => new(DataProtectionProvider.Create(directory, builder => builder.SetApplicationName("Robolex")));
            var encrypted = Create().Protect(user, token);
            Assert.DoesNotContain(token, encrypted);
            Assert.Equal(token, Create().Unprotect(user, encrypted));
            Assert.Throws<CryptographicException>(() => Create().Unprotect(Guid.NewGuid(), encrypted));
            var tampered = encrypted[..^8] + "AAAAAAAA";
            Assert.Throws<CryptographicException>(() => Create().Unprotect(user, tampered));
        }
        finally { directory.Delete(true); }
    }
}
