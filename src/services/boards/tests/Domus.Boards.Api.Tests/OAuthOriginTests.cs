using Domus.Boards.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

public sealed class OAuthOriginTests
{
    [Fact]
    public async Task MicrosoftChallengeRedirectsToRegisteredHttpsCallback()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PortalOrigin"] = "https://domus.mouras.me",
            ["ConnectionStrings:Postgres"] = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
            ["Authentication:Microsoft:ClientId"] = "00000000-0000-0000-0000-000000000001",
            ["Authentication:Microsoft:ClientSecret"] = "synthetic-secret-for-tests"
        });
        Assert.True(builder.AddDomusBoards()["microsoft"]);
        await using var app = builder.Build();
        app.UseMiddleware<OAuthOriginMiddleware>(); app.UseAuthentication();
        app.MapGet("/api/auth/microsoft/start", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, new[] { "microsoft" })).AllowAnonymous();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/auth/microsoft/start");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var uri = response.Headers.Location!;
        Assert.Equal("login.microsoftonline.com", uri.Host);
        Assert.Equal("https://domus.mouras.me/api/auth/microsoft/callback", QueryHelpers.ParseQuery(uri.Query)["redirect_uri"].ToString());
    }
    [Theory]
    [InlineData("/api/auth/microsoft/start")]
    [InlineData("/api/auth/microsoft/callback")]
    [InlineData("/api/auth/google/start")]
    [InlineData("/api/auth/google/callback")]
    public async Task SocialAuthenticationUsesConfiguredPublicOriginInsteadOfProxyHttp(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("internal-service", 5190);
        context.Request.Headers["X-Forwarded-Proto"] = "untrusted";
        var middleware = new OAuthOriginMiddleware(_ => Task.CompletedTask, Options.Create(new ApiOptions { PortalOrigin = "https://domus.mouras.me" }));
        await middleware.InvokeAsync(context);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal("domus.mouras.me", context.Request.Host.Value);
    }
    [Fact]
    public async Task UnrelatedRequestsKeepTheirOriginalOrigin()
    {
        var context = new DefaultHttpContext(); context.Request.Path = "/api/auth/login";
        context.Request.Scheme = "http"; context.Request.Host = new("localhost", 8088);
        await new OAuthOriginMiddleware(_ => Task.CompletedTask, Options.Create(new ApiOptions { PortalOrigin = "https://domus.mouras.me" })).InvokeAsync(context);
        Assert.Equal("http", context.Request.Scheme); Assert.Equal("localhost:8088", context.Request.Host.Value);
    }
}
