using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261118040000_V3_04AddInternalApprovedMapStatus")]
public sealed class V3_04AddInternalApprovedMapStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TYPE system.farm_base_map_status ADD VALUE IF NOT EXISTS 'INTERNAL_APPROVED' BEFORE 'PUBLISHED';",
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL does not safely support removing an enum value in-place.
        // Keep rollback non-destructive; a deliberate type-rebuild migration is required if removal is ever needed.
    }
}
