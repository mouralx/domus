using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kanbada.Api.Persistence.Migrations;

[DbContext(typeof(KanbadaDbContext))]
[Migration("20261010020000_DomusClientPalette")]
public sealed class DomusClientPalette : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE platform_branding
        SET "primary" = '#0095d2', accent = '#00c6f7',
            light_background = '#f1fbff', dark_background = '#071e2a',
            sidebar_background = '#075978', version = version + 1
        WHERE name = 'domus' AND "primary" = '#2563eb' AND accent = '#bfdbfe'
            AND sidebar_background = '#12366b' AND light_background = '#f3f7ff'
            AND dark_background = '#0b1630';
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
