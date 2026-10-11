using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Domus.Boards.Api.Persistence.Migrations;

[DbContext(typeof(BoardsDbContext))]
[Migration("20261010010000_DomusBlueBranding")]
public sealed class DomusBlueBranding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE platform_branding
        SET name = 'domus', "primary" = '#2563eb', accent = '#bfdbfe',
            light_background = '#f3f7ff', dark_background = '#0b1630',
            sidebar_background = '#12366b', version = version + 1
        WHERE version = 0 AND name IN ('Domus', 'domus')
            AND "primary" = '#334153' AND accent = '#aac2e1';
        UPDATE platform_branding SET name = 'domus', version = version + 1 WHERE name = 'Domus';
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
