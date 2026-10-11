using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Domus.Boards.Api;
/// <summary>Owns the developer reference and transport details exposed in OpenAPI.</summary>
public static class ApiDocumentation
{
    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "Domus API";
            document.Info.Version = "v1";
            // Keep Scalar requests on the portal origin when the API sits behind a proxy.
            document.Servers = [new OpenApiServer { Url = "/", Description = "Current origin (portal proxy or direct API)" }];
            document.Info.Description = "## Quick start\n1. POST /api/auth/register (once) or /api/auth/login with your account.\n2. Open **Cards → Create a card**, set id to studio, and send `{\"title\":\"My first card\"}`.\n3. Copy the returned card ID to GET /api/workspaces/{id}/cards/{cardId}.\n4. For edits, PATCH /api/workspaces/{id} with only changed fields and the latest If-Match version. Apply the returned changes locally.\n\nAll examples use fictional data. Replace sample IDs/tokens with values returned by your API. Read-only and no-body endpoints include full URL examples.\n\nBusiness endpoints, branding reads and this developer reference require an authenticated session. Login/registration and minimal authentication bootstrap routes are explicit exceptions. Health probes are local-only. Sign in with POST /api/auth/login first; the browser sends the HttpOnly session cookie on same-origin requests. Mutations require X-Domus-Request: 1. Workspace saves also require the latest version in If-Match. Card and workflow writes are atomic workspace transactions.";
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(OperationDocumentation.Transform);
    }

    public static void MapBoardsDocumentation(this WebApplication app)
    {
        app.MapOpenApi("/api/openapi.json").RequireAuthorization();
        app.MapScalarApiReference("/api/scalar", options => options.WithTitle("Domus API").WithOpenApiRoutePattern("/api/openapi.json").WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient).DisableDefaultFonts().DisableAgent()).RequireAuthorization();
    }
}
