using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Domus.Boards.Api;

public sealed record AssistantItemChange(string Collection, string Operation, string? Id, long ExpectedVersion, JsonObject Fields);

public static class AssistantWorkspaceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/domus/workspaces/{id}/data", async (string id, string collection, int? offset, int? limit, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await resolver.WorkspaceId(id, Auth.User(ctx));
            if (!await db.Members.AnyAsync(m => m.WorkspaceId == workspace && m.UserId == Auth.User(ctx), ct)) throw new ApiError(404, "Workspace not found or access denied.");
            var start = offset ?? 0; var count = limit ?? 20;
            if (start < 0 || count is < 1 or > 50) throw new ApiError(400, "Invalid page size.");
            async Task<JsonArray> Page<T>(IQueryable<T> query) => JsonSerializer.SerializeToNode(await query.Skip(start).Take(count + 1).ToArrayAsync(ct), JsonSerializerOptions.Web)!.AsArray();
            var rows = collection switch
            {
                "projects" => await Page(db.Projects.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Id).Select(p => new { p.Id, p.Name, p.Icon, p.Archived, description = p.Description.Substring(0, Math.Min(p.Description.Length, 500)) })),
                "members" => await Page(db.Members.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Email).Select(p => new { p.Email, p.Name, p.Initials })),
                "statuses" => await Page(db.Statuses.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Position).Select(p => new { p.Id, p.Name, p.Color, p.Complete })),
                "buckets" => await Page(db.Buckets.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Position).Select(p => new { p.Id, p.Name, p.Color })),
                "labels" => await Page(db.Labels.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Position).Select(p => new { p.Id, p.Name, p.Color })),
                "swimlanes" => await Page(db.Swimlanes.AsNoTracking().Where(p => p.WorkspaceId == workspace).OrderBy(p => p.Position).Select(p => new { p.Id, p.Name, p.Color, project = p.ProjectId })),
                _ => throw new ApiError(400, "Unknown workspace collection.")
            };
            var more = rows.Count > count; if (more) rows.RemoveAt(count);
            var version = await db.Workspaces.Where(w => w.Id == workspace).Select(w => w.Version).SingleAsync(ct);
            return Results.Ok(new { version, collection, items = rows, nextOffset = more ? (int?)(start + count) : null });
        }).ExcludeFromDescription();
        api.MapPost("/domus/workspaces/{id}/items/change", async (string id, AssistantItemChange input, HttpContext ctx, WorkspaceResolver resolver, WorkspaceStore store) =>
        {
            if (input.Collection is not ("tasks" or "projects" or "members" or "statuses" or "buckets" or "labels" or "swimlanes") || input.Operation is not ("create" or "update" or "delete")) throw new ApiError(400, "Unknown workspace action.");
            if (input.Fields.ToJsonString().Length > 8000 || input.Fields.Any(p => p.Key is "id" or "userId" or "invitationToken" or "history" or "readOnly" or "system")) throw new ApiError(400, "Invalid or server-controlled fields.");
            var allowed = input.Collection switch
            {
                "tasks" => new[] { "title", "description", "priority", "status", "project", "due", "bucket", "swimlane", "labels", "assignees", "comments", "checklist" },
                "projects" => new[] { "name", "description", "icon", "archived" },
                "members" => new[] { "name", "email", "initials", "color" },
                "swimlanes" => new[] { "name", "color", "complete", "project" },
                _ => new[] { "name", "color", "complete" }
            };
            if (input.Fields.Any(field => !allowed.Contains(field.Key)) || (input.Collection == "members" && input.Operation == "update" && input.Fields.ContainsKey("email"))) throw new ApiError(400, "Unsupported field for this action.");
            var actor = Auth.User(ctx); var workspace = await resolver.WorkspaceId(id, actor);
            var snapshot = await store.Read(workspace, actor);
            if (snapshot.Version != input.ExpectedVersion) throw new ApiError(409, "Workspace changed. Ask the assistant to prepare the action again.");
            var state = snapshot.State; var items = WorkspaceJson.Items(state, input.Collection);
            var key = input.Collection == "members" ? "email" : "id";
            JsonObject item;
            if (input.Operation == "create")
            {
                if (input.Collection == "tasks") throw new ApiError(400, "Use create_card to create cards.");
                item = new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["color"] = "#167d8d", ["complete"] = false };
                if (input.Collection == "projects") { item["description"] = ""; item["icon"] = "folder"; }
                if (input.Collection == "members") item.Remove("id");
                items.Add(item);
            }
            else
            {
                item = items.FirstOrDefault(p => WorkspaceJson.Text(p, key) == input.Id) ?.AsObject() ?? throw new ApiError(404, "Item not found.");
                if (input.Operation == "delete")
                {
                    if (input.Collection == "projects")
                    {
                        foreach (var card in WorkspaceJson.Items(state, "tasks").Where(p => WorkspaceJson.Text(p, "project") == input.Id).ToArray()) WorkspaceJson.Items(state, "tasks").Remove(card);
                        foreach (var lane in WorkspaceJson.Items(state, "swimlanes").Where(p => WorkspaceJson.Text(p, "project") == input.Id).ToArray()) WorkspaceJson.Items(state, "swimlanes").Remove(lane);
                    }
                    foreach (var card in WorkspaceJson.Items(state, "tasks"))
                    {
                        if (input.Collection is "labels" or "members")
                        {
                            var field = input.Collection == "labels" ? "labels" : "assignees";
                            var name = WorkspaceJson.Text(item, "name");
                            if (card![field] is JsonArray links) foreach (var link in links.Where(p => p?.GetValue<string>() == name).ToArray()) links.Remove(link);
                        }
                        if (input.Collection is "buckets" or "swimlanes")
                        {
                            var field = input.Collection == "buckets" ? "bucket" : "swimlane";
                            if (WorkspaceJson.Text(card, field) == input.Id) card![field] = "";
                        }
                        if (input.Collection == "statuses" && WorkspaceJson.Text(card, "status") == WorkspaceJson.Text(item, "name"))
                            card!["status"] = WorkspaceJson.Text(items.FirstOrDefault(p => WorkspaceJson.Text(p, "id") != input.Id), "name");
                    }
                    items.Remove(item);
                }
            }
            if (input.Operation != "delete")
            {
                var previousName = WorkspaceJson.Text(item, "name");
                foreach (var field in input.Fields) item[field.Key] = field.Value?.DeepClone();
                var nextName = WorkspaceJson.Text(item, "name");
                if (input.Operation == "update" && nextName != previousName)
                    foreach (var card in WorkspaceJson.Items(state, "tasks"))
                    {
                        if (input.Collection == "statuses" && WorkspaceJson.Text(card, "status") == previousName) card!["status"] = nextName;
                        if (input.Collection is "labels" or "members")
                        {
                            var field = input.Collection == "labels" ? "labels" : "assignees";
                            if (card![field] is JsonArray links) for (var i = 0; i < links.Count; i++) if (links[i]?.GetValue<string>() == previousName) links[i] = nextName;
                        }
                    }
            }
            var saved = await store.Save(workspace, actor, state, snapshot.Version);
            return Results.Ok(new { ok = true, id = WorkspaceJson.Text(item, key), version = saved["version"]!.GetValue<long>() });
        }).ExcludeFromDescription();
    }
}
