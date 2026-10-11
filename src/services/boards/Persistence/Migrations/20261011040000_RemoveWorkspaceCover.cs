using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011040000_RemoveWorkspaceCover")]
public sealed class RemoveWorkspaceCover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("ALTER TABLE workspaces DROP COLUMN IF EXISTS banner, DROP COLUMN IF EXISTS banner_position;");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("ALTER TABLE workspaces ADD COLUMN banner text, ADD COLUMN banner_position double precision;");
}
