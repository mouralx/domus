using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011050000_ProjectDefaultSwimlanes")]
public sealed class ProjectDefaultSwimlanes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        WITH added AS (
            INSERT INTO swimlanes (workspace_id,id,project_id,name,color,complete,position)
            SELECT p.workspace_id, 'lane-' || gen_random_uuid()::text, p.id, first.name, first.color, false,
                (SELECT coalesce(max(position),-1) FROM swimlanes l WHERE l.workspace_id=p.workspace_id)
                + row_number() OVER (PARTITION BY p.workspace_id ORDER BY p.position,p.id)::integer
            FROM projects p CROSS JOIN LATERAL (
                SELECT name,color FROM swimlanes l WHERE l.workspace_id=p.workspace_id ORDER BY position,id LIMIT 1
            ) first
            WHERE NOT EXISTS (SELECT 1 FROM swimlanes l WHERE l.workspace_id=p.workspace_id AND l.project_id=p.id)
            RETURNING workspace_id
        ) UPDATE workspaces SET version=version+1 WHERE id IN (SELECT workspace_id FROM added);
        WITH changed AS (
            UPDATE cards c SET swimlane_id=(SELECT l.id FROM swimlanes l WHERE l.workspace_id=c.workspace_id AND l.project_id=c.project_id ORDER BY position,id LIMIT 1)
            WHERE swimlane_id IS NULL AND EXISTS (SELECT 1 FROM swimlanes l WHERE l.workspace_id=c.workspace_id AND l.project_id=c.project_id)
            RETURNING workspace_id
        ) UPDATE workspaces SET version=version+1 WHERE id IN (SELECT workspace_id FROM changed);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
