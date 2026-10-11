using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Domus.Boards.Api.Persistence.Migrations;
[DbContext(typeof(BoardsDbContext))]
[Migration("20261011020000_DomusNaming")]
public sealed class DomusNaming : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE legacy_column text;
        BEGIN
            -- Discover the former product-prefixed columns without depending on its name.
            SELECT column_name INTO legacy_column FROM information_schema.columns
              WHERE table_schema = current_schema() AND table_name = 'jira_mappings'
              AND column_name LIKE '%\_value' ESCAPE '\' AND column_name NOT IN ('jira_value', 'domus_value');
            IF legacy_column IS NOT NULL THEN
                EXECUTE format('ALTER TABLE jira_mappings RENAME COLUMN %I TO domus_value', legacy_column);
            END IF;
            SELECT column_name INTO legacy_column FROM information_schema.columns
              WHERE table_schema = current_schema() AND table_name = 'jira_links'
              AND column_name LIKE '%\_hash' ESCAPE '\' AND column_name NOT IN ('jira_hash', 'domus_hash');
            IF legacy_column IS NOT NULL THEN
                EXECUTE format('ALTER TABLE jira_links RENAME COLUMN %I TO domus_hash', legacy_column);
            END IF;
        END $$;
        UPDATE jira_connections SET direction = 'jira-to-domus' WHERE direction LIKE 'jira-to-%';
        UPDATE jira_connections SET direction = 'domus-to-jira' WHERE direction LIKE '%-to-jira';
        UPDATE jira_links SET origin = 'domus' WHERE origin <> 'jira';
        UPDATE platform_branding SET name = 'Domus' WHERE version = 0;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
