using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_05EInvoiceFinalizationAndCorrection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE system.survey_payment_status ADD VALUE IF NOT EXISTS 'INVALIDATED';
                """,
                suppressTransaction: true);

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidated_at",
                schema: "survey",
                table: "survey_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invalidated_by",
                schema: "survey",
                table: "survey_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invalidation_reason",
                schema: "survey",
                table: "survey_payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "superseded_by_invoice_revision_id",
                schema: "survey",
                table: "survey_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments",
                sql: "status IN ('PENDING'::system.survey_payment_status, 'INVALIDATED'::system.survey_payment_status) OR provider_reference IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_invoice_purpose",
                schema: "survey",
                table: "survey_payments",
                sql: "invoice_revision_id IS NULL OR purpose = 'FinalBalance'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_invalidation_audit",
                schema: "survey",
                table: "survey_payments",
                sql: "(status <> 'INVALIDATED'::system.survey_payment_status AND invalidated_at IS NULL AND invalidated_by IS NULL AND invalidation_reason IS NULL AND superseded_by_invoice_revision_id IS NULL) OR (status = 'INVALIDATED'::system.survey_payment_status AND invalidated_at IS NOT NULL AND invalidated_by IS NOT NULL AND invalidation_reason IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_survey_payments_invoice_revision",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "invoice_revision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_payments_superseded_by_invoice_revision",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "superseded_by_invoice_revision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_payments_invalidated_by",
                schema: "survey",
                table: "survey_payments",
                column: "invalidated_by");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_payments_invoice_revision",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "invoice_revision_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_invoice_revisions",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_payments_superseded_by_invoice_revision",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "superseded_by_invoice_revision_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_invoice_revisions",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_payments_invalidated_by",
                schema: "survey",
                table: "survey_payments",
                column: "invalidated_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_result_review_operations_action",
                schema: "survey",
                table: "survey_result_review_operations");

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_result_review_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_result_review_operations_action",
                schema: "survey",
                table: "survey_result_review_operations",
                sql: "action IN ('RecordComboReview', 'FreezeInternalResult', 'FinalizeInvoice', 'CorrectPreReleaseInvoice')");

            migrationBuilder.CreateIndex(
                name: "ix_survey_result_review_operations_invoice_revision",
                schema: "survey",
                table: "survey_result_review_operations",
                columns: new[] { "invoice_revision_id", "survey_order_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_survey_result_review_operations_invoice_revision",
                schema: "survey",
                table: "survey_result_review_operations",
                columns: new[] { "invoice_revision_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_invoice_revisions",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_payments_id_order",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "id", "survey_order_id" });

            migrationBuilder.AddColumn<Guid>(
                name: "survey_payment_id",
                schema: "survey",
                table: "survey_funding_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_funding_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reconciliation_reason",
                schema: "survey",
                table: "survey_funding_receipts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_funding_receipts_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts",
                sql: "(requires_reconciliation = false AND reconciliation_reason IS NULL) OR (requires_reconciliation = true AND reconciliation_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_funding_receipts_invoice_requires_payment",
                schema: "survey",
                table: "survey_funding_receipts",
                sql: "invoice_revision_id IS NULL OR survey_payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_receipts_payment_order",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "survey_payment_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_receipts_invoice_order",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "invoice_revision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_receipts_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "requires_reconciliation", "survey_order_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_survey_funding_receipts_payment_order",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "survey_payment_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_payments",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_funding_receipts_invoice_order",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "invoice_revision_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_invoice_revisions",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_order_pole_count_snapshots_lineage",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                sql: "(revision_number = 1 AND predecessor_snapshot_id IS NULL) OR (revision_number > 1 AND predecessor_snapshot_id IS NOT NULL AND predecessor_snapshot_id <> id)");

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_pole_count_snapshots_predecessor_scope",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "predecessor_snapshot_id", "survey_order_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_survey_order_pole_count_snapshots_predecessor_scope",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "predecessor_snapshot_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_order_pole_count_snapshots",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_survey_order_pole_count_snapshots_predecessor_scope",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_survey_order_pole_count_snapshots_predecessor_scope",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_order_pole_count_snapshots_lineage",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_funding_receipts_invoice_order",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_funding_receipts_payment_order",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropIndex(
                name: "ix_survey_funding_receipts_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropIndex(
                name: "ix_survey_funding_receipts_invoice_order",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropIndex(
                name: "ix_survey_funding_receipts_payment_order",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_funding_receipts_invoice_requires_payment",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_funding_receipts_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropColumn(
                name: "reconciliation_reason",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropColumn(
                name: "requires_reconciliation",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropColumn(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropColumn(
                name: "survey_payment_id",
                schema: "survey",
                table: "survey_funding_receipts");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_payments_id_order",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_result_review_operations_invoice_revision",
                schema: "survey",
                table: "survey_result_review_operations");

            migrationBuilder.DropIndex(
                name: "ix_survey_result_review_operations_invoice_revision",
                schema: "survey",
                table: "survey_result_review_operations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_result_review_operations_action",
                schema: "survey",
                table: "survey_result_review_operations");

            migrationBuilder.DropColumn(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_result_review_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_result_review_operations_action",
                schema: "survey",
                table: "survey_result_review_operations",
                sql: "action IN ('RecordComboReview', 'FreezeInternalResult')");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_payments_invalidated_by",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_payments_superseded_by_invoice_revision",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_payments_invoice_revision",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropIndex(
                name: "ix_survey_payments_invalidated_by",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropIndex(
                name: "ix_survey_payments_superseded_by_invoice_revision",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropIndex(
                name: "ix_survey_payments_invoice_revision",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_invalidation_audit",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_invoice_purpose",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "superseded_by_invoice_revision_id",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "invalidation_reason",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "invalidated_by",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "invalidated_at",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "invoice_revision_id",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_provider_session",
                schema: "survey",
                table: "survey_payments",
                sql: "status = 'PENDING'::system.survey_payment_status OR provider_reference IS NOT NULL");
        }
    }
}
