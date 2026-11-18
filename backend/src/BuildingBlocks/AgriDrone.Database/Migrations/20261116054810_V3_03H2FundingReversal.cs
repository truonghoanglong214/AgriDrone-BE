using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_03H2FundingReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_funding_reversals",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_funding_reversals", x => x.id);
                    table.CheckConstraint("ck_survey_funding_reversals_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_survey_funding_reversals_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_funding_reversals_time", "created_at >= occurred_at");
                    table.ForeignKey(
                        name: "fk_survey_funding_reversals_obligation_order",
                        columns: x => new { x.deposit_obligation_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_deposit_obligations",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_funding_reversals_payment",
                        column: x => x.survey_payment_id,
                        principalSchema: "survey",
                        principalTable: "survey_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_reversals_deposit_obligation_id_survey_order~",
                schema: "survey",
                table: "survey_funding_reversals",
                columns: new[] { "deposit_obligation_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_reversals_order_occurred",
                schema: "survey",
                table: "survey_funding_reversals",
                columns: new[] { "survey_order_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_reversals_survey_payment_id",
                schema: "survey",
                table: "survey_funding_reversals",
                column: "survey_payment_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_funding_reversals_provider_event",
                schema: "survey",
                table: "survey_funding_reversals",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_funding_reversals",
                schema: "survey");
        }
    }
}
