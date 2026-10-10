using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kanbada.Api.Persistence.Migrations;

[DbContext(typeof(KanbadaDbContext))]
[Migration("20261010030000_DomusRefinedPalette")]
public sealed class DomusRefinedPalette : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE platform_branding
        SET "primary" = '#167d8d', accent = '#a8ded6',
            light_background = '#f5f7fa', dark_background = '#101923',
            sidebar_background = '#172b3a', version = version + 1
        WHERE name = 'domus' AND "primary" = '#0095d2' AND accent = '#00c6f7'
            AND light_background = '#f1fbff' AND dark_background = '#071e2a'
            AND sidebar_background = '#075978';
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE platform_branding
        SET "primary" = '#0095d2', accent = '#00c6f7',
            light_background = '#f1fbff', dark_background = '#071e2a',
            sidebar_background = '#075978', version = version + 1
        WHERE name = 'domus' AND "primary" = '#167d8d' AND accent = '#a8ded6'
            AND light_background = '#f5f7fa' AND dark_background = '#101923'
            AND sidebar_background = '#172b3a';
        """);
}
