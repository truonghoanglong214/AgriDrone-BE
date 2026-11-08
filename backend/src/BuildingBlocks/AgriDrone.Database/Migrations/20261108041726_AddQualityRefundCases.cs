using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityRefundCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidated_at",
                schema: "survey",
                table: "survey_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invalidated_by",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invalidated_by_quality_refund_case_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quality_refund_cases",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_issues = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approved_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    bank_transfer_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    transfer_evidence_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    transferred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quality_refund_cases", x => x.id);
                    table.CheckConstraint("ck_quality_refund_approved_amount", "approved_amount IS NULL OR approved_amount > 0");
                    table.CheckConstraint("ck_quality_refund_transfer", "(status = 'Refunded' AND bank_transfer_reference IS NOT NULL AND transfer_evidence_reference IS NOT NULL AND transferred_at IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL) OR (status <> 'Refunded' AND bank_transfer_reference IS NULL AND transferred_at IS NULL)");
                    table.ForeignKey(
                        name: "FK_quality_refund_cases_survey_orders_survey_order_id",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quality_refund_cases_survey_results_survey_result_id",
                        column: x => x.survey_result_id,
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_results_invalidated_by_quality_refund_case_id",
                schema: "survey",
                table: "survey_results",
                column: "invalidated_by_quality_refund_case_id",
                unique: true,
                filter: "invalidated_by_quality_refund_case_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_quality_refund_cases_survey_result_id",
                schema: "survey",
                table: "quality_refund_cases",
                column: "survey_result_id");

            migrationBuilder.CreateIndex(
                name: "IX_quality_refund_cases_tenant_id_created_at",
                schema: "survey",
                table: "quality_refund_cases",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_quality_refund_active_order",
                schema: "survey",
                table: "quality_refund_cases",
                column: "survey_order_id",
                unique: true,
                filter: "status IN ('Submitted', 'Approved', 'Refunded')");

            migrationBuilder.CreateIndex(
                name: "uq_quality_refund_bank_reference",
                schema: "survey",
                table: "quality_refund_cases",
                column: "bank_transfer_reference",
                unique: true,
                filter: "bank_transfer_reference IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quality_refund_cases",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "IX_survey_results_invalidated_by_quality_refund_case_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "invalidated_at",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "invalidated_by",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "invalidated_by_quality_refund_case_id",
                schema: "survey",
                table: "survey_results");
        }
    }
}
