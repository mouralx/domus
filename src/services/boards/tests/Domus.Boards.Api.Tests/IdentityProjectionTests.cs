using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public sealed class IdentityProjectionTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture fixture;
    private const string Secret = "synthetic-identity-sync-secret-for-tests-only";
    public IdentityProjectionTests(ApiFixture fixture) => this.fixture = fixture;
    [Fact]
    public async Task SignedProjectionIsIdempotentAndCannotAcceptAnUnsignedOrExpiredRequest()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["DOMUS_IDENTITY_SYNC_SECRET"] = Secret })));
        using var client = factory.CreateClient();
        var actor = Guid.NewGuid();
        var path = $"/internal/membership/identities/{actor}";
        var payload = JsonSerializer.Serialize(new { Id=actor, Email="synthetic-"+actor+"@example.test",Name="Synthetic Member",Photo=(string?)null,CreatedAt=DateTimeOffset.UtcNow });
        async Task<HttpResponseMessage> Deliver(long timestamp, bool signed)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,path);
            request.Headers.Add("X-Domus-Request","1");
            request.Headers.Add("X-Domus-Timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(signed)request.Headers.Add("X-Domus-Signature",Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),Encoding.UTF8.GetBytes(timestamp+"\n"+path+"\n"+payload))));
            request.Content=new StringContent(payload,Encoding.UTF8,"application/json");
            return await client.SendAsync(request);
        }
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.Equal(HttpStatusCode.Forbidden,(await Deliver(now,false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Deliver(now-120,true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Deliver(now,true)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Deliver(now,true)).StatusCode);
        await using var db=new Npgsql.NpgsqlConnection(fixture.Connection);await db.OpenAsync();
        await using var command=new Npgsql.NpgsqlCommand("SELECT count(*) FROM workspaces WHERE owner_id=@id AND personal",db);command.Parameters.AddWithValue("id",actor);
        Assert.Equal(1L,await command.ExecuteScalarAsync());
        await using var projection=new Npgsql.NpgsqlCommand("SELECT password_hash IS NULL AND two_factor_secret IS NULL FROM users WHERE id=@id",db);projection.Parameters.AddWithValue("id",actor);
        Assert.Equal(true,await projection.ExecuteScalarAsync());
    }
}
