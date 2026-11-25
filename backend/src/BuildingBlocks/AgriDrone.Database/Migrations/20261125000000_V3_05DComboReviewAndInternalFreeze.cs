using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_05DComboReviewAndInternalFreeze : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_combo_review_revisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    combo_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision = table.Column<int>(type: "integer", nullable: false),
                    batch_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_review_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    confirm_no_disease_observed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    negative_result_confirmation_evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    findings_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_findings_count = table.Column<int>(type: "integer", nullable: false),
                    excluded_findings_count = table.Column<int>(type: "integer", nullable: false),
                    zones_count = table.Column<int>(type: "integer", nullable: false),
                    approved_zones_count = table.Column<int>(type: "integer", nullable: false),
                    rejected_zones_count = table.Column<int>(type: "integer", nullable: false),
                    blockers_count = table.Column<int>(type: "integer", nullable: false),
                    resolved_blockers_count = table.Column<int>(type: "integer", nullable: false),
                    unresolved_blockers_count = table.Column<int>(type: "integer", nullable: false),
                    decisions_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    decision_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    superseded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    successor_review_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_combo_review_revisions", x => x.id);
                    table.UniqueConstraint("uq_survey_combo_review_revisions_id_batch_order_tenant_farm", x => new { x.id, x.combo_batch_id, x.survey_order_id, x.tenant_id, x.farm_id });
                    table.UniqueConstraint("uq_survey_combo_review_revisions_id_batch", x => new { x.id, x.combo_batch_id });
                    table.UniqueConstraint("uq_survey_combo_review_revisions_batch_revision", x => new { x.combo_batch_id, x.revision_number });
                    table.CheckConstraint("ck_survey_combo_review_revisions_source_revision", "source_revision >= 1");
                    table.CheckConstraint("ck_survey_combo_review_revisions_revision", "revision_number >= 1");
                    table.CheckConstraint("ck_survey_combo_review_revisions_lineage", "(revision_number = 1 AND predecessor_review_id IS NULL) OR (revision_number > 1 AND predecessor_review_id IS NOT NULL AND predecessor_review_id <> id)");
                    table.CheckConstraint("ck_survey_combo_review_revisions_batch_fingerprint", "batch_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_survey_combo_review_revisions_decision_fingerprint", "decision_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_survey_combo_review_revisions_outcome", "outcome IN ('Approved', 'Rejected')");
                    table.CheckConstraint(
                        "ck_survey_combo_review_revisions_counts",
                        "findings_count >= 0 AND accepted_findings_count >= 0 AND excluded_findings_count >= 0 AND accepted_findings_count + excluded_findings_count = findings_count AND " +
                        "zones_count >= 0 AND approved_zones_count >= 0 AND rejected_zones_count >= 0 AND approved_zones_count + rejected_zones_count = zones_count AND " +
                        "blockers_count >= 0 AND resolved_blockers_count >= 0 AND unresolved_blockers_count >= 0 AND resolved_blockers_count + unresolved_blockers_count = blockers_count");
                    table.CheckConstraint("ck_survey_combo_review_revisions_approved_blockers", "outcome <> 'Approved' OR unresolved_blockers_count = 0");
                    table.CheckConstraint(
                        "ck_survey_combo_review_revisions_supersede_audit",
                        "(is_current = true AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_review_id IS NULL) OR " +
                        "(is_current = false AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_review_id IS NOT NULL AND successor_review_id <> id)");
                    table.ForeignKey(
                        name: "fk_survey_combo_review_revisions_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_review_revisions_batch",
                        columns: x => new { x.combo_batch_id, x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_combo_import_batches",
                        principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_review_revisions_predecessor",
                        columns: x => new { x.predecessor_review_id, x.combo_batch_id },
                        principalSchema: "survey",
                        principalTable: "survey_combo_review_revisions",
                        principalColumns: new[] { "id", "combo_batch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_review_revisions_attempt",
                        columns: x => new { x.capture_attempt_id, x.tenant_id, x.farm_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_review_revisions_reviewer",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_review_revisions_one_current",
                schema: "survey",
                table: "survey_combo_review_revisions",
                column: "combo_batch_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_review_revisions_predecessor_unique",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "combo_batch_id", "predecessor_review_id" },
                unique: true,
                filter: "predecessor_review_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_order_attempt",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "survey_order_id", "capture_attempt_id", "combo_batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_decision_fingerprint",
                schema: "survey",
                table: "survey_combo_review_revisions",
                column: "decision_fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_batch",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_order",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_predecessor",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "predecessor_review_id", "combo_batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_attempt",
                schema: "survey",
                table: "survey_combo_review_revisions",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_review_revisions_reviewer",
                schema: "survey",
                table: "survey_combo_review_revisions",
                column: "reviewed_by");

            migrationBuilder.CreateTable(
                name: "survey_result_review_operations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    api_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    combo_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_result_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pole_count_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_result_review_operations", x => x.id);
                    table.CheckConstraint("ck_survey_result_review_operations_fingerprint", "request_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_survey_result_review_operations_action", "action IN ('RecordComboReview', 'FreezeInternalResult')");
                    table.ForeignKey(
                        name: "fk_survey_result_review_operations_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_result_review_operations_actor",
                        column: x => x.actor_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_result_review_operations_actor_action_op",
                schema: "survey",
                table: "survey_result_review_operations",
                columns: new[] { "tenant_id", "actor_id", "action", "api_version", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_result_review_operations_order",
                schema: "survey",
                table: "survey_result_review_operations",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_result_review_operations_actor",
                schema: "survey",
                table: "survey_result_review_operations",
                column: "actor_id");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_rules",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.AddColumn<Guid>(
                name: "combo_review_revision_id",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "combo_review_fingerprint",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_rules",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                sql: "((service_code = 'MAP' AND NOT screens_disease AND combo_batch_id IS NULL AND combo_review_revision_id IS NULL AND combo_review_fingerprint IS NULL AND health_assessment_status = 'NotAssessed' AND limitation_notice IS NULL) OR (service_code = 'COMBO' AND screens_disease AND combo_batch_id IS NOT NULL AND combo_review_revision_id IS NOT NULL AND combo_review_fingerprint IS NOT NULL AND health_assessment_status IN ('SuspectedObservationsApproved', 'NoObservationsDetected') AND limitation_notice IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_review_fingerprint",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                sql: "combo_review_fingerprint IS NULL OR combo_review_fingerprint ~ '^[0-9a-f]{64}$'");

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_combo_review",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "combo_review_revision_id", "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_survey_internal_approved_result_revisions_combo_review",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "combo_review_revision_id", "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_combo_review_revisions",
                principalColumns: new[] { "id", "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_survey_internal_approved_result_revisions_combo_review",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropIndex(
                name: "ix_survey_internal_approved_result_revisions_combo_review",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_review_fingerprint",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_rules",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropColumn(
                name: "combo_review_revision_id",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropColumn(
                name: "combo_review_fingerprint",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_internal_approved_result_revisions_combo_rules",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                sql: "((service_code = 'MAP' AND NOT screens_disease AND combo_batch_id IS NULL AND health_assessment_status = 'NotAssessed' AND limitation_notice IS NULL) OR (service_code = 'COMBO' AND screens_disease AND combo_batch_id IS NOT NULL AND health_assessment_status IN ('SuspectedObservationsApproved', 'NoObservationsDetected') AND limitation_notice IS NOT NULL))");

            migrationBuilder.DropTable(
                name: "survey_result_review_operations",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_combo_review_revisions",
                schema: "survey");
        }
    }
}
