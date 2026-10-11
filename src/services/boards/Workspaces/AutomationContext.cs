using Microsoft.EntityFrameworkCore;

namespace Domus.Boards.Api;

public static class AutomationContext
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/domus/platform/context", (HttpContext ctx, PlatformAdmins admins) =>
            Results.Ok(new { userId = Auth.User(ctx), canManagePlatform = admins.Contains(Auth.User(ctx)) })).ExcludeFromDescription();
        api.MapGet("/domus/workspaces/{id}/context", async (string id, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, PlatformAdmins admins) =>
        {
            var user = Auth.User(ctx);
            var workspace = await resolver.WorkspaceId(id, user);
            var owner = await db.Workspaces.AsNoTracking()
                .Where(x => x.Id == workspace && db.Members.Any(m => m.WorkspaceId == workspace && m.UserId == user))
                .Select(x => (Guid?)x.OwnerId).SingleOrDefaultAsync()
                ?? throw new ApiError(404, "Workspace not found or access denied.");
            return Results.Ok(new { userId = user, workspaceId = workspace, canManage = owner == user, canManagePlatform = admins.Contains(user) });
        }).ExcludeFromDescription();
    }
}
