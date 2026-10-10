using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261010110000_SnapshotMissionFarmBoundaryVersion")]
public sealed class SnapshotMissionFarmBoundaryVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "farm_boundary_version_id",
            schema: "mission",
            table: "drone_missions",
            type: "uuid",
            nullable: true);

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "farm_boundary_version_id",
            schema: "mission",
            table: "drone_missions");
    }
}
