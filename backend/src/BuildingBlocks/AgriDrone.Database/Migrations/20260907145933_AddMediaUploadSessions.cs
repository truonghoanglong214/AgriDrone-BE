using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_upload_sessions",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    media_type = table.Column<int>(type: "system.media_type", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expected_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_uri = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_upload_sessions", x => x.id);
                    table.CheckConstraint("ck_upload_sessions_checksum", "checksum_algorithm = 'SHA256' AND expected_checksum ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_upload_sessions_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_upload_sessions_file_size", "file_size_bytes > 0");
                    table.CheckConstraint("ck_upload_sessions_status", "status IN ('Pending', 'Verifying', 'Completed', 'Failed', 'Expired', 'Aborted')");
                    table.CheckConstraint("ck_upload_sessions_updated_at", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "fk_upload_sessions_mission_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_upload_sessions_mission_tenant",
                        columns: x => new { x.mission_id, x.tenant_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_mission_id_farm_id",
                schema: "mission",
                table: "media_upload_sessions",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_mission_id_tenant_id",
                schema: "mission",
                table: "media_upload_sessions",
                columns: new[] { "mission_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_cleanup",
                schema: "mission",
                table: "media_upload_sessions",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_upload_sessions_media_asset",
                schema: "mission",
                table: "media_upload_sessions",
                column: "media_asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_upload_sessions_operation",
                schema: "mission",
                table: "media_upload_sessions",
                columns: new[] { "tenant_id", "mission_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_upload_sessions_storage_uri",
                schema: "mission",
                table: "media_upload_sessions",
                column: "storage_uri",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_upload_sessions",
                schema: "mission");
        }
    }
}
