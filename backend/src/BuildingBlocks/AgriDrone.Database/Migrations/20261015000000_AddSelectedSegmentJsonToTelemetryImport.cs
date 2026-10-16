using AgriDrone.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261015000000_AddSelectedSegmentJsonToTelemetryImport")]
public partial class AddSelectedSegmentJsonToTelemetryImport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<System.Text.Json.JsonDocument>(
            name: "selected_segment_json",
            schema: "mission",
            table: "mission_telemetry_imports",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_telemetry_imports_selected_segment_object",
            schema: "mission",
            table: "mission_telemetry_imports",
            sql: "selected_segment_json IS NULL OR jsonb_typeof(selected_segment_json) = 'object'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_telemetry_imports_selected_segment_object",
            schema: "mission",
            table: "mission_telemetry_imports");

        migrationBuilder.DropColumn(
            name: "selected_segment_json",
            schema: "mission",
            table: "mission_telemetry_imports");
    }
}
