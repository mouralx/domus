using Microsoft.EntityFrameworkCore;

namespace Domus.Boards.Api;

// Narrow, bounded reads for the MCP tools. Never load or map the workspace state.
public static class AssistantReadEndpoints
{
    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max] + "…";
    private static async Task<Guid> Access(string id, string project, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct)
    {
        var workspace = await resolver.WorkspaceId(id, Auth.User(ctx));
        if (!await db.Members.AnyAsync(m => m.WorkspaceId == workspace && m.UserId == Auth.User(ctx), ct)
            || !await db.Projects.AnyAsync(p => p.WorkspaceId == workspace && p.Id == project && !p.Archived, ct))
            throw new ApiError(404, "Project not found or access denied.");
        return workspace;
    }
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/domus/workspaces/{id}/projects/{project}/context", async (string id, string project, bool? includeMetrics, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, WorkspaceMetrics metrics, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, ctx, resolver, db, ct);
            var details = await db.Projects.AsNoTracking().Where(p => p.WorkspaceId == workspace && p.Id == project)
                .Select(p => new { p.Id, p.Name, p.Description }).SingleAsync(ct);
            var member = await db.Members.Where(m => m.WorkspaceId == workspace && m.UserId == Auth.User(ctx)).Select(m => m.Name).SingleAsync(ct);
            return Results.Ok(new { project = new { details.Id, name = Clip(details.Name, 240), description = Clip(details.Description, 500) }, currentMember = member, today = DateOnly.FromDateTime(DateTime.UtcNow), metrics = includeMetrics == false ? null : await metrics.Calculate(workspace, Auth.User(ctx), DateOnly.FromDateTime(DateTime.UtcNow), project) });
        }).ExcludeFromDescription();
        api.MapGet("/domus/workspaces/{id}/projects/{project}/cards/search", async (string id, string project, string? q, string? status, string? priority, bool? completed, bool? overdue, string? assignee, string? cursor, int? limit, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, ctx, resolver, db, ct);
            if ((q?.Length ?? 0) > 200 || (cursor?.Length ?? 0) > 100 || (assignee?.Length ?? 0) > 100 || (status?.Length ?? 0) > 100 || (priority?.Length ?? 0) > 20 || limit is < 1 or > 50)
                throw new ApiError(400, "Invalid search filters or page size (1–50).");
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var query = from card in db.Cards.AsNoTracking()
                        join definition in db.Statuses on new { card.WorkspaceId, Id = card.StatusId } equals new { definition.WorkspaceId, definition.Id }
                        where card.WorkspaceId == workspace && card.ProjectId == project
                        select new { card, status = definition.Name, definition.Complete };
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ToTsVector("simple", x.card.Title + " " + x.card.Description).Matches(EF.Functions.PlainToTsQuery("simple", q)));
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.status == status);
            if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.card.Priority == priority);
            if (completed is not null) query = query.Where(x => x.Complete == completed);
            if (overdue == true) query = query.Where(x => !x.Complete && x.card.Due != null && x.card.Due < today);
            if (!string.IsNullOrWhiteSpace(assignee)) query = query.Where(x => db.CardAssignees.Any(a => a.WorkspaceId == workspace && a.CardId == x.card.Id && db.Members.Any(m => m.WorkspaceId == workspace && m.Email == a.MemberEmail && m.Name == assignee)));
            if (!string.IsNullOrWhiteSpace(cursor)) query = query.Where(x => string.Compare(x.card.Id, cursor) > 0);
            var count = limit ?? 20;
            var rows = await query.OrderBy(x => x.card.Id).Select(x => new { x.card.Id, x.card.Title, description = x.card.Description.Substring(0, Math.Min(x.card.Description.Length, 350)), x.status, x.card.Priority, x.card.Due }).Take(count + 1).ToListAsync(ct);
            var page = rows.Take(count).Select(x => new { x.Id, title = Clip(x.Title, 240), x.description, x.status, x.Priority, x.Due }).ToArray();
            return Results.Ok(new { cards = page, nextCursor = rows.Count > count ? page.Last().Id : null, hasMore = rows.Count > count, searchedFields = new[] { "title", "description" } });
        }).ExcludeFromDescription();
        api.MapGet("/domus/workspaces/{id}/projects/{project}/cards/access", async (string id, string project, string? ids, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, ctx, resolver, db, ct);
            var requested = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            if (requested.Length > 30 || requested.Any(value => value.Length > 100)) throw new ApiError(400, "Too many card references.");
            var cards = await db.Cards.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.ProjectId == project && requested.Contains(c.Id)).Select(c => new { c.Id, c.Title }).ToArrayAsync(ct);
            return Results.Ok(new { cards = cards.Select(c => new { c.Id, title = Clip(c.Title, 240) }) });
        }).ExcludeFromDescription();
        api.MapGet("/domus/workspaces/{id}/projects/{project}/cards/{card}/context", async (string id, string project, string card, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, ctx, resolver, db, ct);
            var row = await (from c in db.Cards.AsNoTracking() join s in db.Statuses on new { c.WorkspaceId, Id = c.StatusId } equals new { s.WorkspaceId, s.Id }
                             where c.WorkspaceId == workspace && c.ProjectId == project && c.Id == card
                             select new { c.Id, c.Title, description = c.Description.Substring(0, Math.Min(c.Description.Length, 4000)), status = s.Name, c.Priority, c.Due }).SingleOrDefaultAsync(ct)
                ?? throw new ApiError(404, "Card not found or access denied.");
            var assignees = await (from a in db.CardAssignees join m in db.Members on new { a.WorkspaceId, Email = a.MemberEmail } equals new { m.WorkspaceId, m.Email }
                                   where a.WorkspaceId == workspace && a.CardId == card orderby a.Position select m.Name).Take(20).ToArrayAsync(ct);
            var comments = await db.CardComments.Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderByDescending(c => c.Position).Select(c => c.Text.Substring(0, Math.Min(c.Text.Length, 500))).Take(5).ToArrayAsync(ct);
            var checklist = await db.ChecklistItems.Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderBy(c => c.Position).Select(c => new { text = c.Text.Substring(0, Math.Min(c.Text.Length, 250)), c.Done }).Take(20).ToArrayAsync(ct);
            var checklistTotal = await db.ChecklistItems.CountAsync(c => c.WorkspaceId == workspace && c.CardId == card, ct);
            var checklistDone = await db.ChecklistItems.CountAsync(c => c.WorkspaceId == workspace && c.CardId == card && c.Done, ct);
            return Results.Ok(new { row.Id, title = Clip(row.Title, 240), row.description, row.status, row.Priority, row.Due, assignees = assignees.Select(a => Clip(a, 100)), comments, checklist, checklistTotal, checklistDone, excerpts = true });
        }).ExcludeFromDescription();
    }
}
