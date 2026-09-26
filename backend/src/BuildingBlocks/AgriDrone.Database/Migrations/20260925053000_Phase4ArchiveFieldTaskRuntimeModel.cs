using AgriDrone.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20260925053000_Phase4ArchiveFieldTaskRuntimeModel")]
public sealed class Phase4ArchiveFieldTaskRuntimeModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retention approval is intentionally not inferred from a zero-row local
        // inventory. The project/runtime mapping is removed while this schema is
        // left intact as unmanaged archival data.
        migrationBuilder.Sql(
            "COMMENT ON SCHEMA field_task IS " +
            "'ARCHIVAL ONLY: detached from the AgriDrone runtime in Be-Plan Phase 4';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("COMMENT ON SCHEMA field_task IS NULL;");
    }
}
