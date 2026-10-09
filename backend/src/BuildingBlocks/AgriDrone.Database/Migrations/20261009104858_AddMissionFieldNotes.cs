using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionFieldNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mission_field_notes",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_field_notes", x => x.id);
                    table.CheckConstraint("ck_mission_field_notes_text", "length(trim(text)) > 0");
                    table.ForeignKey(
                        name: "fk_mission_field_notes_mission_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_field_notes_mission_tenant",
                        columns: x => new { x.mission_id, x.tenant_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mission_field_notes_mission_id_farm_id",
                schema: "mission",
                table: "mission_field_notes",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_field_notes_mission_id_tenant_id",
                schema: "mission",
                table: "mission_field_notes",
                columns: new[] { "mission_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mission_field_notes_timeline",
                schema: "mission",
                table: "mission_field_notes",
                columns: new[] { "mission_id", "observed_at" });

            migrationBuilder.CreateIndex(
                name: "uq_mission_field_notes_operation",
                schema: "mission",
                table: "mission_field_notes",
                columns: new[] { "mission_id", "operation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_field_notes",
                schema: "mission");

        }
    }
}
