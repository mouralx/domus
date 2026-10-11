using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011030000_DefaultCardPlacement")]
public sealed class DefaultCardPlacement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        WITH changed AS (
            UPDATE cards c SET bucket_id = (SELECT b.id FROM buckets b WHERE b.workspace_id=c.workspace_id ORDER BY b.position,b.id LIMIT 1)
            WHERE c.bucket_id IS NULL AND EXISTS (SELECT 1 FROM buckets b WHERE b.workspace_id=c.workspace_id)
            RETURNING c.workspace_id
        ) UPDATE workspaces SET version=version+1 WHERE id IN (SELECT workspace_id FROM changed);
        WITH changed AS (
            UPDATE cards c SET swimlane_id = (SELECT l.id FROM swimlanes l WHERE l.workspace_id=c.workspace_id AND l.project_id=c.project_id ORDER BY l.position,l.id LIMIT 1)
            WHERE c.swimlane_id IS NULL AND EXISTS (SELECT 1 FROM swimlanes l WHERE l.workspace_id=c.workspace_id AND l.project_id=c.project_id)
            RETURNING c.workspace_id
        ) UPDATE workspaces SET version=version+1 WHERE id IN (SELECT workspace_id FROM changed);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
