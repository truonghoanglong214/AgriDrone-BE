using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_03H2AppointmentNoShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_appointment_no_shows",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    disposition = table.Column<string>(type: "character varying(40)", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_appointment_no_shows", x => x.id);
                    table.CheckConstraint("ck_survey_appointment_no_shows_occurrence", "occurrence_index > 0");
                    table.ForeignKey(
                        name: "fk_survey_appointment_no_shows_appointment",
                        column: x => x.survey_appointment_id,
                        principalSchema: "survey",
                        principalTable: "survey_appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_appointment_no_shows_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointment_no_shows_appointment",
                schema: "survey",
                table: "survey_appointment_no_shows",
                column: "survey_appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointment_no_shows_operation",
                schema: "survey",
                table: "survey_appointment_no_shows",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointment_no_shows_order_occurrence",
                schema: "survey",
                table: "survey_appointment_no_shows",
                columns: new[] { "survey_order_id", "occurrence_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_appointment_no_shows",
                schema: "survey");
        }
    }
}
