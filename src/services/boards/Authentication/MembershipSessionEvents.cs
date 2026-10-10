using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Kanbada.Api;

public sealed record MembershipSession(Guid UserId, string Name, string Email, bool AvatarPending, bool TwoFactorPending, bool CanManagePlatform);
public sealed class MembershipSessionEvents : CookieAuthenticationEvents
{
    private readonly HttpClient client = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Environment.GetEnvironmentVariable("DOMUS_MEMBERSHIP_URL") + "/api/membership/session/context");
        request.Headers.Host = new Uri(Environment.GetEnvironmentVariable("PortalOrigin")!).Authority;
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToString());
        using var response = await client.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode) { context.RejectPrincipal(); return; }
        var identity = await response.Content.ReadFromJsonAsync<MembershipSession>(context.HttpContext.RequestAborted);
        if (identity is null || identity.UserId == Guid.Empty) { context.RejectPrincipal(); return; }
        context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, identity.UserId.ToString()), new Claim(ClaimTypes.Name, identity.Name),
            new Claim(ClaimTypes.Email, identity.Email), new Claim("avatar_pending", identity.AvatarPending ? "true" : "false"),
            new Claim("two_factor_pending", identity.TwoFactorPending ? "true" : "false"), new Claim("platform_admin", identity.CanManagePlatform ? "true" : "false")
        }, "session")));
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) { context.Response.StatusCode = 401; return Task.CompletedTask; }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) { context.Response.StatusCode = 403; return Task.CompletedTask; }
}
