using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Domus.Boards.Api.Persistence.Migrations;

[DbContext(typeof(BoardsDbContext))]
[Migration("20261009230000_DomusDefaultBranding")]
public sealed class DomusDefaultBranding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("UPDATE platform_branding SET name = 'Domus' WHERE name = 'domus' AND version = 0;");
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
