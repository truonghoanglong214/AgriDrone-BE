using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    public partial class V3_05EFundingReversalReceiptLinkage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "funding_receipt_id", schema: "survey", table: "survey_funding_reversals",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "cash_amount", schema: "survey", table: "survey_funding_reversals",
                type: "numeric(18,2)", precision: 18, scale: 2, nullable: true);
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_funding_reversals_amount", schema: "survey", table: "survey_funding_reversals");
            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_funding_reversals_amount", schema: "survey", table: "survey_funding_reversals",
                sql: "amount = FLOOR(amount) AND ((cash_amount IS NULL AND funding_receipt_id IS NULL AND amount > 0) OR (cash_amount IS NOT NULL AND cash_amount > 0 AND cash_amount = FLOOR(cash_amount) AND amount >= 0 AND amount <= cash_amount AND (funding_receipt_id IS NOT NULL OR amount = 0)))");
            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_reversals_funding_receipt_id_survey_order_id",
                schema: "survey", table: "survey_funding_reversals", columns: new[] { "funding_receipt_id", "survey_order_id" });
            migrationBuilder.AddForeignKey(
                name: "fk_survey_funding_reversals_receipt_order", schema: "survey", table: "survey_funding_reversals",
                columns: new[] { "funding_receipt_id", "survey_order_id" },
                principalSchema: "survey", principalTable: "survey_funding_receipts",
                principalColumns: new[] { "id", "survey_order_id" }, onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_funding_reversals WHERE cash_amount IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back receipt-linked reversals after their cash evidence has been written.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_survey_funding_reversals_receipt_order", schema: "survey", table: "survey_funding_reversals");
            migrationBuilder.DropIndex(
                name: "IX_survey_funding_reversals_funding_receipt_id_survey_order_id",
                schema: "survey", table: "survey_funding_reversals");
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_funding_reversals_amount", schema: "survey", table: "survey_funding_reversals");
            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_funding_reversals_amount", schema: "survey", table: "survey_funding_reversals",
                sql: "amount > 0 AND amount = FLOOR(amount)");
            migrationBuilder.DropColumn(
                name: "funding_receipt_id", schema: "survey", table: "survey_funding_reversals");
            migrationBuilder.DropColumn(
                name: "cash_amount", schema: "survey", table: "survey_funding_reversals");
        }
    }
}
