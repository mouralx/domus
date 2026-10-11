using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011060000_ProjectIcons")]
public sealed class ProjectIcons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("ALTER TABLE projects ADD COLUMN icon text; WITH ranked AS (SELECT workspace_id,id,row_number() OVER (PARTITION BY workspace_id ORDER BY position,id) n FROM projects) UPDATE projects p SET icon=CASE WHEN p.system='activities' THEN 'notebook' ELSE (ARRAY['folder','briefcase','calendar','chart','checklist','compass','check','layers','network','package','shield','target','users'])[((r.n-1)%13+1)::integer] END FROM ranked r WHERE p.workspace_id=r.workspace_id AND p.id=r.id; UPDATE workspaces SET version=version+1 WHERE EXISTS (SELECT 1 FROM projects p WHERE p.workspace_id=workspaces.id);");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("ALTER TABLE projects DROP COLUMN icon;");
}
