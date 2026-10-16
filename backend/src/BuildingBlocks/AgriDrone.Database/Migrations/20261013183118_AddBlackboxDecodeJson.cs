using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBlackboxDecodeJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blackbox_decodes",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_checksum = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    points_json = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blackbox_decodes", x => x.id);
                    table.CheckConstraint("ck_blackbox_decodes_points_object", "jsonb_typeof(points_json) = 'object'");
                    table.ForeignKey(
                        name: "FK_blackbox_decodes_drone_missions_mission_id_farm_id",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_blackbox_decodes_drone_missions_mission_id_tenant_id",
                        columns: x => new { x.mission_id, x.tenant_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blackbox_decodes_mission_id_farm_id",
                schema: "mission",
                table: "blackbox_decodes",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_blackbox_decodes_mission_id_tenant_id",
                schema: "mission",
                table: "blackbox_decodes",
                columns: new[] { "mission_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "uq_blackbox_decodes_mission_checksum",
                schema: "mission",
                table: "blackbox_decodes",
                columns: new[] { "mission_id", "source_checksum" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blackbox_decodes",
                schema: "mission");
        }
    }
}
