using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Domus.Boards.Api;

// Complete records are reachable through pages and resumable JSON/text chunks.
public static class CardDataEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/domus/workspaces/{id}/projects/{project}/cards/{card}/data", async (string id, string project, string card, string section, int? itemOffset, int? itemLimit, int? textOffset, int? textLength, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, card, ctx, resolver, db, ct);
            var start = itemOffset ?? 0; var count = itemLimit ?? 10; var offset = textOffset ?? 0; var length = textLength ?? 8000;
            if (start < 0 || count is < 1 or > 10 || offset < 0 || length is < 1 or > 8000) throw new ApiError(400, "Invalid page or text range.");
            string data; int total;
            async Task<string> Page<T>(IQueryable<T> query)
            {
                total = await query.CountAsync(ct);
                return JsonSerializer.Serialize(await query.Skip(start).Take(count).ToArrayAsync(ct), Json);
            }
            total = 0;
            switch (section)
            {
                case "card":
                    data = await Page(db.Cards.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.Id == card).OrderBy(c => c.Id)); break;
                case "comments":
                    data = await Page(db.CardComments.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderBy(c => c.Position)); break;
                case "checklist":
                    data = await Page(db.ChecklistItems.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderBy(c => c.Position)); break;
                case "labels":
                    data = await Page(from link in db.CardLabels.AsNoTracking() join label in db.Labels on new { link.WorkspaceId, Id = link.LabelId } equals new { label.WorkspaceId, label.Id } where link.WorkspaceId == workspace && link.CardId == card orderby link.Position select label); break;
                case "assignees":
                    data = await Page(from link in db.CardAssignees.AsNoTracking() join member in db.Members on new { link.WorkspaceId, Email = link.MemberEmail } equals new { member.WorkspaceId, member.Email } where link.WorkspaceId == workspace && link.CardId == card orderby link.Position select new { member.Name, member.Email, member.Initials, member.Color }); break;
                case "attachments":
                    data = await Page(from link in db.CardAttachments.AsNoTracking() join file in db.Files on link.FileId equals file.Id where link.WorkspaceId == workspace && link.CardId == card && file.WorkspaceId == workspace orderby link.Position select new { file.Id, file.Name, file.ContentType, size = file.Bytes.Length, file.CreatedAt }); break;
                case "history":
                    data = await Page(db.HistoryEntries.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderBy(c => c.Position)); break;
                case "historyChanges":
                    data = await Page(db.HistoryChanges.AsNoTracking().Where(c => c.WorkspaceId == workspace && c.CardId == card).OrderBy(c => c.HistoryId).ThenBy(c => c.Position)); break;
                default: throw new ApiError(400, "Unknown card section.");
            }
            if (offset > data.Length) throw new ApiError(400, "Text offset is outside the page.");
            var end = Math.Min(data.Length, offset + length);
            return Results.Ok(new { section, itemOffset = start, totalItems = total, nextItemOffset = start + count < total ? (int?)(start + count) : null, textOffset = offset, totalCharacters = data.Length, nextTextOffset = end < data.Length ? (int?)end : null, data = data[offset..end] });
        }).ExcludeFromDescription();
        api.MapGet("/domus/workspaces/{id}/projects/{project}/cards/{card}/attachments/{file:guid}/data", async (string id, string project, string card, Guid file, int? offset, int? length, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct) =>
        {
            var workspace = await Access(id, project, card, ctx, resolver, db, ct);
            var start = offset ?? 0; var count = length ?? 12000;
            if (start < 0 || count is < 1 or > 12000) throw new ApiError(400, "Invalid byte range.");
            var result = await db.Database.SqlQuery<AttachmentChunk>($"""
                SELECT f.name AS "Name", f.content_type AS "ContentType", octet_length(f.bytes) AS "Size",
                       substring(f.bytes from LEAST({start}, octet_length(f.bytes)) + 1 for {count}) AS "Bytes"
                FROM files f WHERE f.id = {file} AND f.workspace_id = {workspace}
                  AND EXISTS (SELECT 1 FROM card_attachments a WHERE a.workspace_id = {workspace} AND a.card_id = {card} AND a.file_id = f.id)
                """).SingleOrDefaultAsync(ct) ?? throw new ApiError(404, "Attachment not found or access denied.");
            if (start > result.Size) throw new ApiError(400, "Byte offset is outside the file.");
            var end = Math.Min(result.Size, start + count);
            return Results.Ok(new { result.Name, result.ContentType, size = result.Size, offset = start, nextOffset = end < result.Size ? (int?)end : null, encoding = "base64", data = Convert.ToBase64String(result.Bytes) });
        }).ExcludeFromDescription();
    }
    public sealed class AttachmentChunk
    {
        public string Name { get; set; } = "";
        public string ContentType { get; set; } = "";
        public int Size { get; set; }
        public byte[] Bytes { get; set; } = [];
    }
    private static async Task<Guid> Access(string id, string project, string card, HttpContext ctx, WorkspaceResolver resolver, BoardsDbContext db, CancellationToken ct)
    {
        var workspace = await resolver.WorkspaceId(id, Auth.User(ctx));
        if (!await db.Members.AnyAsync(m => m.WorkspaceId == workspace && m.UserId == Auth.User(ctx), ct) || !await db.Projects.AnyAsync(p => p.WorkspaceId == workspace && p.Id == project && !p.Archived, ct) || !await db.Cards.AnyAsync(c => c.WorkspaceId == workspace && c.ProjectId == project && c.Id == card, ct)) throw new ApiError(404, "Card not found or access denied.");
        return workspace;
    }
}
