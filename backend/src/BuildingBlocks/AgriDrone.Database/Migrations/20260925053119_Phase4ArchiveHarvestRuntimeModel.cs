using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

public sealed partial class Phase4ArchiveHarvestRuntimeModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Harvest data has no valid conversion to Harvest Readiness. Keep the
        // legacy schema intact until a production-like inventory and retention
        // approval authorize a separate contract migration.
        migrationBuilder.Sql(
            "COMMENT ON SCHEMA harvest IS " +
            "'ARCHIVAL ONLY: detached from the AgriDrone runtime in Be-Plan Phase 4';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("COMMENT ON SCHEMA harvest IS NULL;");
    }
}
