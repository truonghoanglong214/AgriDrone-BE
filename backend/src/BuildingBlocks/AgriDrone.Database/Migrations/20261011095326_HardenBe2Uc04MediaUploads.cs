using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class HardenBe2Uc04MediaUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "captured_at",
                schema: "mission",
                table: "media_upload_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_upload_sessions_active_checksum",
                schema: "mission",
                table: "media_upload_sessions",
                columns: new[] { "tenant_id", "mission_id", "expected_checksum" },
                unique: true,
                filter: "status IN ('Pending', 'Verifying', 'Completed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_upload_sessions_active_checksum",
                schema: "mission",
                table: "media_upload_sessions");

            migrationBuilder.DropColumn(
                name: "captured_at",
                schema: "mission",
                table: "media_upload_sessions");
        }
    }
}
