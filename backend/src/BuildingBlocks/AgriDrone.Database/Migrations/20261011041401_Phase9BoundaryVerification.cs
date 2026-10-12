using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

/// <inheritdoc />
public partial class Phase9BoundaryVerification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TYPE system.farm_boundary_source
            ADD VALUE IF NOT EXISTS 'SYSTEM_MANAGER' AFTER 'TENANT_OWNER';
            """);

        migrationBuilder.AddColumn<string>(
            name: "evidence_reference",
            schema: "farm",
            table: "farm_boundaries",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "evidence_reference",
            schema: "farm",
            table: "farm_boundaries");

        // PostgreSQL cannot remove an enum label safely. The additive
        // SYSTEM_MANAGER value is intentionally retained on downgrade.
    }
}
