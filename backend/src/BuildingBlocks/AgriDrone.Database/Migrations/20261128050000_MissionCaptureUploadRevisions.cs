using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgriDrone.Database.Migrations;

[DbContext(typeof(AgriDroneSchemaDbContext))]
[Migration("20261128050000_MissionCaptureUploadRevisions")]
public sealed class MissionCaptureUploadRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "drone_missions", "media_upload_sessions", "mission_telemetry_imports" })
        {
            migrationBuilder.AddColumn<Guid>("upload_revision_id", table,
                schema: "mission", type: "uuid", nullable: false, defaultValue: Guid.Empty);
        }

        migrationBuilder.DropIndex("uq_upload_sessions_active_checksum", "media_upload_sessions", "mission");
        migrationBuilder.CreateIndex("uq_upload_sessions_active_checksum", "media_upload_sessions",
            new[] { "tenant_id", "mission_id", "upload_revision_id", "expected_checksum" }, schema: "mission",
            unique: true, filter: "status IN ('Pending', 'Verifying', 'Completed')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("uq_upload_sessions_active_checksum", "media_upload_sessions", "mission");
        migrationBuilder.CreateIndex("uq_upload_sessions_active_checksum", "media_upload_sessions",
            new[] { "tenant_id", "mission_id", "expected_checksum" }, schema: "mission",
            unique: true, filter: "status IN ('Pending', 'Verifying', 'Completed')");

        foreach (var table in new[] { "drone_missions", "media_upload_sessions", "mission_telemetry_imports" })
        {
            migrationBuilder.DropColumn("upload_revision_id", table, "mission");
        }
    }
}
