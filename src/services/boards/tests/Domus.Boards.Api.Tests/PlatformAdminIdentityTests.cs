using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Domus.Boards.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class PlatformAdminIdentityTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture fixture;
    public PlatformAdminIdentityTests(ApiFixture fixture) => this.fixture = fixture;
    [Fact]
    public async Task ExplicitAdministratorIdDoesNotGrantPrivilegesToAnotherIdentityWithTheSameEmail()
    {
        using var client = await fixture.AuthenticatedClient();
        var user = (await client.GetFromJsonAsync<JsonObject>("/api/auth/session"))!["user"]!;
        var original = Guid.Parse(user["id"]!.GetValue<string>()); var email = user["email"]!.GetValue<string>();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BoardsDbContext>();
        var external = Guid.NewGuid(); db.Users.Add(new UserEntity { Id = external, Email = email, Name = "External identity" }); await db.SaveChangesAsync();
        var admins = new PlatformAdmins();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:AdminUserIds"] = original.ToString(), ["Platform:AdminEmails"] = email }).Build();
        await admins.Initialize(config, db);
        Assert.True(admins.Contains(original)); Assert.False(admins.Contains(external));
        var unknown = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:AdminUserIds"] = Guid.NewGuid().ToString() }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PlatformAdmins().Initialize(unknown, db));
        var ambiguous = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:AdminEmails"] = email }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PlatformAdmins().Initialize(ambiguous, db));
    }
}
