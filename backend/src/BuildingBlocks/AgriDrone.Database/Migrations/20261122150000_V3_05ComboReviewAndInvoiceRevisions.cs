using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_05ComboReviewAndInvoiceRevisions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_order_tenant_farm",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_order",
                schema: "survey",
                table: "survey_order_pole_count_snapshots",
                columns: new[] { "id", "survey_order_id" });

            migrationBuilder.CreateTable(
                name: "survey_internal_approved_result_revisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    screens_disease = table.Column<bool>(type: "boolean", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pole_count_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_candidate_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    combo_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    health_assessment_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    limitation_notice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    successor_revision_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_internal_approved_result_revisions", x => x.id);
                    table.UniqueConstraint("uq_survey_internal_approved_result_revisions_composite_scope", x => new { x.id, x.survey_order_id, x.tenant_id, x.farm_id });
                    table.UniqueConstraint("uq_survey_internal_approved_result_revisions_id_order", x => new { x.id, x.survey_order_id });
                    table.UniqueConstraint("uq_survey_internal_approved_result_revisions_order_revision", x => new { x.survey_order_id, x.revision_number });
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_revision", "revision_number >= 1");
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_lineage",
                        "(revision_number = 1 AND predecessor_revision_id IS NULL) OR " +
                        "(revision_number > 1 AND predecessor_revision_id IS NOT NULL AND predecessor_revision_id <> id)");
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_service_code", "service_code IN ('MAP', 'COMBO')");
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_combo_rules",
                        "((service_code = 'MAP' AND NOT screens_disease AND combo_batch_id IS NULL AND health_assessment_status = 'NotAssessed' AND limitation_notice IS NULL) OR " +
                        "(service_code = 'COMBO' AND screens_disease AND combo_batch_id IS NOT NULL AND health_assessment_status IN ('SuspectedObservationsApproved', 'NoObservationsDetected') AND limitation_notice IS NOT NULL))");
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_supersede_audit",
                        "(is_current = true AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_revision_id IS NULL) OR " +
                        "(is_current = false AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_revision_id IS NOT NULL AND successor_revision_id <> id)");
                    table.CheckConstraint("ck_survey_internal_approved_result_revisions_fingerprint", "evidence_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_snapshot_scope",
                        columns: x => new { x.pole_count_snapshot_id, x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_order_pole_count_snapshots",
                        principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_predecessor_scope",
                        columns: x => new { x.predecessor_revision_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_internal_approved_result_revisions",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_attempt",
                        columns: x => new { x.capture_attempt_id, x.tenant_id, x.farm_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_mission",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_batch",
                        columns: x => new { x.mapping_candidate_batch_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_base_map",
                        columns: x => new { x.farm_base_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_baseline_map",
                        columns: x => new { x.baseline_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_boundary",
                        columns: x => new { x.farm_boundary_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_internal_approved_result_revisions_reviewer",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_invoice_revisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_result_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pole_count_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_billable_pole_count = table.Column<int>(type: "integer", nullable: false),
                    survey_service_price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_per_pole = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "VND"),
                    commercial_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valid_allocated_deposit_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    overpayment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    evidence_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalization_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "CURRENT"),
                    zero_pole_approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    zero_pole_reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    zero_pole_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    successor_revision_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_invoice_revisions", x => x.id);
                    table.UniqueConstraint("uq_survey_invoice_revisions_composite_scope", x => new { x.id, x.survey_order_id, x.tenant_id, x.farm_id });
                    table.UniqueConstraint("uq_survey_invoice_revisions_id_order", x => new { x.id, x.survey_order_id });
                    table.UniqueConstraint("uq_survey_invoice_revisions_order_revision", x => new { x.survey_order_id, x.revision_number });
                    table.CheckConstraint("ck_survey_invoice_revisions_revision", "revision_number >= 1");
                    table.CheckConstraint("ck_survey_invoice_revisions_lineage",
                        "(revision_number = 1 AND predecessor_revision_id IS NULL) OR " +
                        "(revision_number > 1 AND predecessor_revision_id IS NOT NULL AND predecessor_revision_id <> id)");
                    table.CheckConstraint("ck_survey_invoice_revisions_pole_count", "verified_billable_pole_count >= 0");
                    table.CheckConstraint("ck_survey_invoice_revisions_price", "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                    table.CheckConstraint("ck_survey_invoice_revisions_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_invoice_revisions_amounts",
                        "total_amount >= 0 AND total_amount = FLOOR(total_amount) AND " +
                        "total_amount = verified_billable_pole_count * price_per_pole AND " +
                        "valid_allocated_deposit_net >= 0 AND valid_allocated_deposit_net = FLOOR(valid_allocated_deposit_net) AND " +
                        "due_amount >= 0 AND due_amount = FLOOR(due_amount) AND " +
                        "due_amount = GREATEST(0, total_amount - valid_allocated_deposit_net) AND " +
                        "overpayment_amount >= 0 AND overpayment_amount = FLOOR(overpayment_amount) AND " +
                        "overpayment_amount = GREATEST(0, valid_allocated_deposit_net - total_amount)");
                    table.CheckConstraint("ck_survey_invoice_revisions_zero_pole",
                        "(verified_billable_pole_count > 0 AND zero_pole_approved_by IS NULL AND zero_pole_reason_code IS NULL AND zero_pole_evidence_reference IS NULL) OR " +
                        "(verified_billable_pole_count = 0 AND zero_pole_approved_by IS NOT NULL AND zero_pole_reason_code IS NOT NULL AND zero_pole_evidence_reference IS NOT NULL)");
                    table.CheckConstraint("ck_survey_invoice_revisions_fingerprint", "evidence_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_survey_invoice_revisions_status", "status IN ('CURRENT', 'SUPERSEDED')");
                    table.CheckConstraint("ck_survey_invoice_revisions_supersede_audit",
                        "(status = 'CURRENT' AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_revision_id IS NULL) OR " +
                        "(status = 'SUPERSEDED' AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_revision_id IS NOT NULL AND successor_revision_id <> id)");
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_approved_result_scope",
                        columns: x => new { x.approved_result_revision_id, x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_internal_approved_result_revisions",
                        principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_snapshot_scope",
                        columns: x => new { x.pole_count_snapshot_id, x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_order_pole_count_snapshots",
                        principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_predecessor_scope",
                        columns: x => new { x.predecessor_revision_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_invoice_revisions",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_service_price",
                        column: x => x.survey_service_price_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_policy_version",
                        column: x => x.commercial_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_users_finalized_by",
                        column: x => x.finalized_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_invoice_revisions_users_zero_pole_approved_by",
                        column: x => x.zero_pole_approved_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "current_approved_result_revision_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_invoice_revision_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_attempt",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_mission",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_batch",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "mapping_candidate_batch_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_base_map",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_baseline_map",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "baseline_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_boundary",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_reviewer",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_snapshot_scope",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "pole_count_snapshot_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_predecessor_scope",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "predecessor_revision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_order",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_internal_approved_result_revisions_one_current",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                column: "survey_order_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_service_price",
                schema: "survey",
                table: "survey_invoice_revisions",
                column: "survey_service_price_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_policy_version",
                schema: "survey",
                table: "survey_invoice_revisions",
                column: "commercial_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_finalized_by",
                schema: "survey",
                table: "survey_invoice_revisions",
                column: "finalized_by");

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_zero_pole_approver",
                schema: "survey",
                table: "survey_invoice_revisions",
                column: "zero_pole_approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_approved_result_scope",
                schema: "survey",
                table: "survey_invoice_revisions",
                columns: new[] { "approved_result_revision_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_snapshot_scope",
                schema: "survey",
                table: "survey_invoice_revisions",
                columns: new[] { "pole_count_snapshot_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_predecessor_scope",
                schema: "survey",
                table: "survey_invoice_revisions",
                columns: new[] { "predecessor_revision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_invoice_revisions_order",
                schema: "survey",
                table: "survey_invoice_revisions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_invoice_revisions_one_current",
                schema: "survey",
                table: "survey_invoice_revisions",
                column: "survey_order_id",
                unique: true,
                filter: "status = 'CURRENT'");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_orders_current_approved_result_scope",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "current_approved_result_revision_id", "id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_internal_approved_result_revisions",
                principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_orders_current_invoice_revision_scope",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "current_invoice_revision_id", "id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_invoice_revisions",
                principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "ix_survey_orders_current_approved_result_scope",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "current_approved_result_revision_id", "id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_orders_current_invoice_revision_scope",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "current_invoice_revision_id", "id", "tenant_id", "farm_id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_survey_orders_current_approved_result_scope",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropIndex(
                name: "ix_survey_orders_current_invoice_revision_scope",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_current_approved_result_scope",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_current_invoice_revision_scope",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "current_approved_result_revision_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "current_invoice_revision_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropTable(
                name: "survey_invoice_revisions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_internal_approved_result_revisions",
                schema: "survey");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_order_tenant_farm",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_order_pole_count_snapshots_id_order",
                schema: "survey",
                table: "survey_order_pole_count_snapshots");
        }
    }
}
