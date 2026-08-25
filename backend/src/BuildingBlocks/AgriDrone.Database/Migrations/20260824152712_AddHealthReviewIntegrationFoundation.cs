using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthReviewIntegrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "health_review_awaiting_field_verification",
                schema: "mission",
                table: "drone_missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "health_review_changed_at",
                schema: "mission",
                table: "drone_missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "health_review_handoff_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "health_review_pending",
                schema: "mission",
                table: "drone_missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "health_review_resolved",
                schema: "mission",
                table: "drone_missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "health_review_state",
                schema: "mission",
                table: "drone_missions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "health_review_total",
                schema: "mission",
                table: "drone_missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "health_review_version",
                schema: "mission",
                table: "drone_missions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_drone_missions_health_review_handoff",
                schema: "mission",
                table: "drone_missions",
                column: "health_review_handoff_id",
                unique: true,
                filter: "health_review_handoff_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drone_missions_health_review_counts",
                schema: "mission",
                table: "drone_missions",
                sql: "health_review_total >= 0 AND health_review_pending >= 0 AND health_review_awaiting_field_verification >= 0 AND health_review_resolved >= 0 AND health_review_pending + health_review_awaiting_field_verification + health_review_resolved = health_review_total");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_drone_missions_health_review_handoff",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_health_review_counts",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_awaiting_field_verification",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_changed_at",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_handoff_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_pending",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_resolved",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_state",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_total",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "health_review_version",
                schema: "mission",
                table: "drone_missions");
        }
    }
}
