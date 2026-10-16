using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImplementBe2Uc03PreflightLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "preflight_checklist_answers",
                schema: "mission",
                table: "drone_missions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preflight_checklist_version",
                schema: "mission",
                table: "drone_missions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preflight_notes",
                schema: "mission",
                table: "drone_missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "preflight_operation_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "preflight_suitable_for_flight",
                schema: "mission",
                table: "drone_missions",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_drone_missions_preflight_operation",
                schema: "mission",
                table: "drone_missions",
                column: "preflight_operation_id",
                unique: true,
                filter: "preflight_operation_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drone_missions_preflight_snapshot",
                schema: "mission",
                table: "drone_missions",
                sql: "(preflight_operation_id IS NULL AND preflight_checklist_version IS NULL AND preflight_checklist_answers IS NULL AND preflight_suitable_for_flight IS NULL) OR (preflight_operation_id IS NOT NULL AND preflight_checklist_version IS NOT NULL AND preflight_checklist_answers IS NOT NULL AND preflight_suitable_for_flight IS NOT NULL AND preflight_confirmed_by IS NOT NULL AND preflight_confirmed_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM mission.drone_missions
                        WHERE preflight_operation_id IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade UC03 while pre-flight snapshots exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "uq_drone_missions_preflight_operation",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_preflight_snapshot",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_checklist_answers",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_checklist_version",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_notes",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_operation_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "preflight_suitable_for_flight",
                schema: "mission",
                table: "drone_missions");
        }
    }
}
