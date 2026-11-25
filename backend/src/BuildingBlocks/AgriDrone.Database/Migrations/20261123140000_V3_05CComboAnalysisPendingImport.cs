using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_05CComboAnalysisPendingImport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_combo_import_batches",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision = table.Column<int>(type: "integer", nullable: false),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_handoff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_approval_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_manifest_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mapping_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mapping_candidate_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    canonical_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    produced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    blocking_count = table.Column<int>(type: "integer", nullable: false),
                    blocking_items = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PENDING_REVIEW")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_combo_import_batches", x => x.id);
                    table.UniqueConstraint("uq_survey_combo_import_batches_id_order_tenant_farm", x => new { x.id, x.survey_order_id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_survey_combo_import_batches_source_revision", "source_revision >= 1");
                    table.CheckConstraint("ck_survey_combo_import_batches_blocking_count", "blocking_count >= 0");
                    table.CheckConstraint("ck_survey_combo_import_batches_fingerprint", "canonical_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_survey_combo_import_batches_status", "status IN ('PENDING_REVIEW', 'REVIEWED', 'SUPERSEDED')");
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_order",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_predecessor",
                        columns: x => new { x.predecessor_batch_id, x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_combo_import_batches",
                        principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_attempt",
                        columns: x => new { x.capture_attempt_id, x.tenant_id, x.farm_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_mission",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_mapping_batch",
                        columns: x => new { x.mapping_candidate_batch_id, x.tenant_id, x.farm_id },
                        principalSchema: "mapping",
                        principalTable: "mapping_candidate_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_base_map",
                        columns: x => new { x.farm_base_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_baseline_map",
                        columns: x => new { x.baseline_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_boundary",
                        columns: x => new { x.farm_boundary_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_combo_import_batches_policy_version",
                        column: x => x.business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_import_batches_tenant_handoff",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "tenant_id", "handoff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_combo_import_batches_business_identity",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "tenant_id", "survey_order_id", "capture_attempt_id", "job_id", "source_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_fingerprint",
                schema: "survey",
                table: "survey_combo_import_batches",
                column: "canonical_fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_order_imported",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "survey_order_id", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_attempt",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_mission",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_mapping_batch",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "mapping_candidate_batch_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_base_map",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_baseline_map",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "baseline_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_boundary",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_policy_version",
                schema: "survey",
                table: "survey_combo_import_batches",
                column: "business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_combo_import_batches_predecessor",
                schema: "survey",
                table: "survey_combo_import_batches",
                columns: new[] { "predecessor_batch_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_internal_approved_result_revisions_combo_batch",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_survey_internal_approved_result_revisions_combo_batch",
                schema: "survey",
                table: "survey_internal_approved_result_revisions",
                columns: new[] { "combo_batch_id", "survey_order_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_combo_import_batches",
                principalColumns: new[] { "id", "survey_order_id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "capture_input_manifest_files",
                schema: "survey",
                columns: table => new
                {
                    manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    media_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_input_manifest_files", x => new { x.manifest_id, x.media_asset_id });
                    table.CheckConstraint("ck_capture_input_manifest_files_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_capture_input_manifest_files_size", "size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_capture_input_manifest_files_manifest",
                        column: x => x.manifest_id,
                        principalSchema: "survey",
                        principalTable: "capture_input_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_manifest_files_manifest",
                schema: "survey",
                table: "capture_input_manifest_files",
                column: "manifest_id");

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_manifest_files_media_asset",
                schema: "survey",
                table: "capture_input_manifest_files",
                column: "media_asset_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capture_input_manifest_files",
                schema: "survey");

            migrationBuilder.DropForeignKey(
                name: "fk_survey_internal_approved_result_revisions_combo_batch",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropIndex(
                name: "ix_survey_internal_approved_result_revisions_combo_batch",
                schema: "survey",
                table: "survey_internal_approved_result_revisions");

            migrationBuilder.DropTable(
                name: "survey_combo_import_batches",
                schema: "survey");
        }
    }
}
