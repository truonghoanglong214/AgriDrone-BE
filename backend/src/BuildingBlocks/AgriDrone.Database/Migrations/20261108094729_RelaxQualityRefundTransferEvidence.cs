using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class RelaxQualityRefundTransferEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quality_refund_transfer",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quality_refund_transfer",
                schema: "survey",
                table: "quality_refund_cases",
                sql: "(status = 'Refunded' AND bank_transfer_reference IS NOT NULL AND transferred_at IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL) OR (status <> 'Refunded' AND bank_transfer_reference IS NULL AND transferred_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quality_refund_transfer",
                schema: "survey",
                table: "quality_refund_cases");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quality_refund_transfer",
                schema: "survey",
                table: "quality_refund_cases",
                sql: "(status = 'Refunded' AND bank_transfer_reference IS NOT NULL AND transfer_evidence_reference IS NOT NULL AND transferred_at IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL) OR (status <> 'Refunded' AND bank_transfer_reference IS NULL AND transferred_at IS NULL)");
        }
    }
}
