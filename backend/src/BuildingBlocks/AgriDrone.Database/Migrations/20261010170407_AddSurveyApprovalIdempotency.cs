using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyApprovalIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_approval_idempotency",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    command_fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    result_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_approval_idempotency", x => x.id);
                    table.CheckConstraint("ck_survey_approval_idempotency_completion", "(result_snapshot IS NULL AND completed_at IS NULL) OR (result_snapshot IS NOT NULL AND completed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_survey_approval_idempotency_requests_request_id",
                        column: x => x.survey_request_id,
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_approval_idempotency_actor_key",
                schema: "survey",
                table: "survey_approval_idempotency",
                columns: new[] { "actor_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_approval_idempotency_request",
                schema: "survey",
                table: "survey_approval_idempotency",
                column: "survey_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_approval_idempotency",
                schema: "survey");
        }
    }
}
