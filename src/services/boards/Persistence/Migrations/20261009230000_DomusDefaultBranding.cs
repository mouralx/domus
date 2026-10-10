using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kanbada.Api.Persistence.Migrations;

[DbContext(typeof(KanbadaDbContext))]
[Migration("20261009230000_DomusDefaultBranding")]
public sealed class DomusDefaultBranding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("UPDATE platform_branding SET name = 'Domus' WHERE name = 'kanbada' AND version = 0;");
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
