using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase9BaselineAppointmentReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL enum labels are intentionally expand-only. The Down
            // migration leaves this harmless label in place.
            migrationBuilder.Sql(
                "ALTER TYPE system.survey_appointment_status " +
                "ADD VALUE IF NOT EXISTS 'SUPERSEDED' AFTER 'CANCELLED';",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.survey_appointments
                        WHERE length(reschedule_reason) > 1000)
                    THEN
                        RAISE EXCEPTION
                            'Phase 9B migration blocked: reschedule_reason exceeds 1000 characters.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM survey.survey_appointments
                        WHERE status = 'RESCHEDULE_REQUESTED'::system.survey_appointment_status
                          AND (reschedule_reason IS NULL OR length(btrim(reschedule_reason)) = 0))
                    THEN
                        RAISE EXCEPTION
                            'Phase 9B migration blocked: a reschedule-requested appointment has no reason.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "reschedule_reason",
                schema: "survey",
                table: "survey_appointments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_appointment_id",
                schema: "survey",
                table: "survey_appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proposed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                schema: "survey",
                table: "survey_appointments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "superseded_at",
                schema: "survey",
                table: "survey_appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH versioned AS (
                    SELECT
                        id,
                        (row_number() OVER (
                            PARTITION BY survey_order_id, purpose
                            ORDER BY created_at, id))::integer AS revision_number,
                        lag(id) OVER (
                            PARTITION BY survey_order_id, purpose
                            ORDER BY created_at, id) AS previous_appointment_id
                    FROM survey.survey_appointments
                )
                UPDATE survey.survey_appointments AS appointment
                SET revision_number = versioned.revision_number,
                    previous_appointment_id =
                        versioned.previous_appointment_id
                FROM versioned
                WHERE appointment.id = versioned.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_survey_appointments_proposed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments",
                column: "proposed_by_system_manager_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointments_previous",
                schema: "survey",
                table: "survey_appointments",
                column: "previous_appointment_id",
                unique: true,
                filter: "previous_appointment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointments_revision",
                schema: "survey",
                table: "survey_appointments",
                columns: new[] { "survey_order_id", "purpose", "revision_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_appointments_reschedule_reason",
                schema: "survey",
                table: "survey_appointments",
                sql: "status <> 'RESCHEDULE_REQUESTED'::system.survey_appointment_status OR (reschedule_reason IS NOT NULL AND length(btrim(reschedule_reason)) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_appointments_revision_chain",
                schema: "survey",
                table: "survey_appointments",
                sql: "(revision_number = 1 AND previous_appointment_id IS NULL) OR (revision_number > 1 AND previous_appointment_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_appointments_superseded",
                schema: "survey",
                table: "survey_appointments",
                sql: "(status = 'SUPERSEDED'::system.survey_appointment_status AND superseded_at IS NOT NULL) OR (status <> 'SUPERSEDED'::system.survey_appointment_status AND superseded_at IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_appointments_previous_appointment_id",
                schema: "survey",
                table: "survey_appointments",
                column: "previous_appointment_id",
                principalSchema: "survey",
                principalTable: "survey_appointments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_appointments_users_proposed_by",
                schema: "survey",
                table: "survey_appointments",
                column: "proposed_by_system_manager_id",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_survey_appointments_previous_appointment_id",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropForeignKey(
                name: "fk_survey_appointments_users_proposed_by",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropIndex(
                name: "IX_survey_appointments_proposed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropIndex(
                name: "uq_survey_appointments_previous",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropIndex(
                name: "uq_survey_appointments_revision",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_appointments_reschedule_reason",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_appointments_revision_chain",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_appointments_superseded",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropColumn(
                name: "previous_appointment_id",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropColumn(
                name: "proposed_by_system_manager_id",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropColumn(
                name: "revision_number",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.DropColumn(
                name: "superseded_at",
                schema: "survey",
                table: "survey_appointments");
            migrationBuilder.AlterColumn<string>(
                name: "reschedule_reason",
                schema: "survey",
                table: "survey_appointments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
