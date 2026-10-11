using Microsoft.Extensions.Options;

namespace Domus.Boards.Api;

// OAuth redirects and token redemption must use the same registered public URI,
// including when the membership service receives plain HTTP behind the portal.
public sealed class OAuthOriginMiddleware(RequestDelegate next, IOptions<ApiOptions> options)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth/microsoft") || context.Request.Path.StartsWithSegments("/api/auth/google"))
        {
            if (!Uri.TryCreate(options.Value.PortalOrigin, UriKind.Absolute, out var origin) || origin.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
                throw new ApiError(503, "Configure a valid public origin for social sign-in.");
            context.Request.Scheme = origin.Scheme;
            context.Request.Host = HostString.FromUriComponent(origin.Authority);
        }
        return next(context);
    }
}
