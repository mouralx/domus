using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011010000_AssistantSearch")]
public sealed class AssistantSearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE INDEX cards_assistant_page ON cards (workspace_id, project_id, id);
        CREATE INDEX cards_assistant_text ON cards USING gin (to_tsvector('simple', title || ' ' || description));
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP INDEX cards_assistant_text; DROP INDEX cards_assistant_page;");
}
