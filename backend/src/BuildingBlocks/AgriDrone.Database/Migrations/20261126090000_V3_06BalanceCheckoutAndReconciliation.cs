using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations;

public sealed partial class V3_06BalanceCheckoutAndReconciliation : Migration
{
    private const string PreviousActiveIndexFilter =
        "status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'PAID'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)";

    private const string LegacyAndDepositActiveIndexFilter =
        "(purpose <> 'FinalBalance' OR invoice_revision_id IS NULL) AND status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'PAID'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)";

    private const string V3OpenBalanceIndexFilter =
        "purpose = 'FinalBalance' AND invoice_revision_id IS NOT NULL AND status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM survey.survey_payments AS payment
                    JOIN survey.survey_orders AS survey_order ON survey_order.id = payment.survey_order_id
                    WHERE payment.purpose = 'FinalBalance'
                      AND ((survey_order.scope = 'V3'::system.contract_scope) <> (payment.invoice_revision_id IS NOT NULL))
                ) THEN
                    RAISE EXCEPTION 'Preflight failed: FinalBalance invoice binding does not match the survey order scope.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM survey.survey_payments
                    WHERE purpose = 'FinalBalance'
                      AND invoice_revision_id IS NOT NULL
                      AND status IN (
                          'PENDING'::system.survey_payment_status,
                          'PROCESSING'::system.survey_payment_status,
                          'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                    GROUP BY survey_order_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Preflight failed: multiple open V3 FinalBalance checkouts exist for a single SurveyOrder.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM survey.survey_payments
                    WHERE (purpose <> 'FinalBalance' OR invoice_revision_id IS NULL)
                      AND status IN (
                          'PENDING'::system.survey_payment_status,
                          'PROCESSING'::system.survey_payment_status,
                          'PAID'::system.survey_payment_status,
                          'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                    GROUP BY survey_order_id, purpose
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Preflight failed: duplicate active or paid legacy/deposit payments exist for a single SurveyOrder and Purpose.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropIndex(
            name: "uq_survey_payments_one_active_per_order_purpose",
            schema: "survey",
            table: "survey_payments");

        migrationBuilder.CreateIndex(
            name: "uq_survey_payments_one_active_per_order_purpose",
            schema: "survey",
            table: "survey_payments",
            columns: new[] { "survey_order_id", "purpose" },
            unique: true,
            filter: LegacyAndDepositActiveIndexFilter);

        migrationBuilder.CreateIndex(
            name: "uq_survey_payments_one_open_v3_balance_per_order",
            schema: "survey",
            table: "survey_payments",
            column: "survey_order_id",
            unique: true,
            filter: V3OpenBalanceIndexFilter);

        migrationBuilder.CreateTable(
            name: "survey_funding_reconciliation_decisions",
            schema: "survey",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                payload_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                disposition_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                survey_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                funding_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                target_invoice_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                provider_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                provider_transaction_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                evidence_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                expected_order_version = table.Column<long>(type: "bigint", nullable: false),
                reconciled_by = table.Column<Guid>(type: "uuid", nullable: false),
                reconciled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_survey_funding_reconciliation_decisions", x => x.id);
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_disposition",
                    "disposition_kind IN ('KeepExcluded', 'AllocateToCurrentInvoice', 'RouteToRefund')");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_amount",
                    "amount >= 0 AND amount = FLOOR(amount) AND (disposition_kind = 'KeepExcluded' OR amount > 0)");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_currency",
                    "currency = 'VND'");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_target",
                    "survey_payment_id IS NOT NULL OR funding_receipt_id IS NOT NULL");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_invoice_target",
                    "disposition_kind <> 'AllocateToCurrentInvoice' OR target_invoice_revision_id IS NOT NULL");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_fingerprint",
                    "payload_fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_order_version",
                    "expected_order_version >= 1");
                table.CheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_time",
                    "created_at >= reconciled_at");
                table.ForeignKey(
                    name: "fk_survey_funding_reconciliation_decisions_order",
                    column: x => x.survey_order_id,
                    principalSchema: "survey",
                    principalTable: "survey_orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_survey_funding_reconciliation_decisions_payment_order",
                    columns: x => new { x.survey_payment_id, x.survey_order_id },
                    principalSchema: "survey",
                    principalTable: "survey_payments",
                    principalColumns: new[] { "id", "survey_order_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_survey_funding_reconciliation_decisions_receipt_order",
                    columns: x => new { x.funding_receipt_id, x.survey_order_id },
                    principalSchema: "survey",
                    principalTable: "survey_funding_receipts",
                    principalColumns: new[] { "id", "survey_order_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_survey_funding_reconciliation_decisions_invoice_order",
                    columns: x => new { x.target_invoice_revision_id, x.survey_order_id },
                    principalSchema: "survey",
                    principalTable: "survey_invoice_revisions",
                    principalColumns: new[] { "id", "survey_order_id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "uq_survey_funding_reconciliation_decisions_order_operation",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            columns: new[] { "survey_order_id", "operation_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_survey_funding_reconciliation_decisions_order_time",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            columns: new[] { "survey_order_id", "reconciled_at", "id" });

        migrationBuilder.CreateIndex(
            name: "uq_survey_reconciliation_final_receipt",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            column: "funding_receipt_id",
            unique: true,
            filter: "funding_receipt_id IS NOT NULL AND disposition_kind IN ('AllocateToCurrentInvoice', 'RouteToRefund')");

        migrationBuilder.CreateIndex(
            name: "ix_survey_funding_reconciliation_decisions_receipt",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            columns: new[] { "funding_receipt_id", "survey_order_id" });

        migrationBuilder.CreateIndex(
            name: "ix_survey_funding_reconciliation_decisions_payment",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            columns: new[] { "survey_payment_id", "survey_order_id" });

        migrationBuilder.CreateIndex(
            name: "ix_survey_funding_reconciliation_decisions_invoice",
            schema: "survey",
            table: "survey_funding_reconciliation_decisions",
            columns: new[] { "target_invoice_revision_id", "survey_order_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "survey_funding_reconciliation_decisions",
            schema: "survey");

        migrationBuilder.DropIndex(
            name: "uq_survey_payments_one_open_v3_balance_per_order",
            schema: "survey",
            table: "survey_payments");

        migrationBuilder.DropIndex(
            name: "uq_survey_payments_one_active_per_order_purpose",
            schema: "survey",
            table: "survey_payments");

        migrationBuilder.CreateIndex(
            name: "uq_survey_payments_one_active_per_order_purpose",
            schema: "survey",
            table: "survey_payments",
            columns: new[] { "survey_order_id", "purpose" },
            unique: true,
            filter: PreviousActiveIndexFilter);
    }
}
