using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step7BPlantHealthHumanReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.AddColumn<long>(
                name: "review_revision",
                schema: "survey",
                table: "survey_results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "review_updated_at",
                schema: "survey",
                table: "survey_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_health_review_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guidance_decision",
                schema: "plant",
                table: "disease_zones",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_plant_condition_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewed_suspected_class_code",
                schema: "plant",
                table: "disease_zones",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_health_import_result_tenant",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "id", "tenant_id", "survey_result_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_health_boundary_review_context",
                schema: "plant",
                table: "plant_health_boundary_exceptions",
                columns: new[] { "id", "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_disease_zones_review_context",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateTable(
                name: "plant_health_review_operations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_review_operations", x => x.id);
                    table.ForeignKey(
                        name: "fk_health_review_operation_batch",
                        columns: x => new { x.batch_id, x.tenant_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_health_reviews",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    boundary_exception_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_review_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_reviews", x => x.id);
                    table.UniqueConstraint("uq_health_review_context", x => new { x.id, x.batch_id, x.kind, x.subject_id, x.tenant_id, x.farm_id, x.survey_result_id });
                    table.CheckConstraint("ck_health_review_decision", "(kind = 'Finding' AND decision IN ('Approve','Correct','Reject','NonDisease','Ambiguous')) OR (kind = 'Zone' AND decision IN ('Approve','Correct','Reject','Ambiguous')) OR (kind = 'Boundary' AND decision IN ('AcceptCorrectedPosition','ExcludeEvidence','NeedsEvidence')) OR (kind = 'Guidance' AND decision IN ('Select','RejectAll','NeedsEvidence'))");
                    table.CheckConstraint("ck_health_review_subject", "(kind = 'Finding' AND finding_id = subject_id AND finding_id IS NOT NULL AND zone_id IS NULL AND boundary_exception_id IS NULL) OR (kind IN ('Zone','Guidance') AND zone_id = subject_id AND zone_id IS NOT NULL AND finding_id IS NULL AND boundary_exception_id IS NULL) OR (kind = 'Boundary' AND boundary_exception_id = subject_id AND boundary_exception_id IS NOT NULL AND finding_id IS NULL AND zone_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_health_review_batch_finding",
                        columns: x => new { x.batch_id, x.finding_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_batch_findings",
                        principalColumns: new[] { "batch_id", "finding_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_review_boundary_context",
                        columns: x => new { x.boundary_exception_id, x.batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_boundary_exceptions",
                        principalColumns: new[] { "id", "batch_id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_review_finding_context",
                        columns: x => new { x.finding_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_findings",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_review_import_context",
                        columns: x => new { x.batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_review_previous",
                        columns: x => new { x.previous_review_id, x.batch_id, x.kind, x.subject_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_reviews",
                        principalColumns: new[] { "id", "batch_id", "kind", "subject_id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_review_zone_context",
                        columns: x => new { x.zone_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "disease_zones",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_health_review_states",
                schema: "plant",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_review_states", x => new { x.batch_id, x.kind, x.subject_id });
                    table.ForeignKey(
                        name: "fk_health_review_state_current",
                        columns: x => new { x.current_review_id, x.batch_id, x.kind, x.subject_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_reviews",
                        principalColumns: new[] { "id", "batch_id", "kind", "subject_id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones",
                sql: "(source_schema_version = 4 AND ((status = 'PROPOSED' AND published_at IS NULL AND published_by IS NULL) OR (status = 'REJECTED' AND current_health_review_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND reviewed_geometry IS NULL AND published_at IS NULL AND published_by IS NULL) OR (status = 'REVIEWED' AND current_health_review_id IS NOT NULL AND reviewed_geometry IS NOT NULL AND reviewed_suspected_class_code IS NOT NULL AND reviewed_plant_condition_id IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND has_blocking_items = FALSE AND (guidance_decision = 'RejectAll' OR (guidance_decision = 'Select' AND selected_sourced_guidance_id IS NOT NULL)) AND published_at IS NULL AND published_by IS NULL))) OR (source_schema_version = 3 AND ((status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_review_operations_batch_id_tenant_id_survey_re~",
                schema: "survey",
                table: "plant_health_review_operations",
                columns: new[] { "batch_id", "tenant_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "uq_health_review_operation",
                schema: "survey",
                table: "plant_health_review_operations",
                columns: new[] { "tenant_id", "actor_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_review_states_current_review_id_batch_id_kind_~",
                schema: "plant",
                table: "plant_health_review_states",
                columns: new[] { "current_review_id", "batch_id", "kind", "subject_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "ix_health_review_history",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "batch_id", "kind", "subject_id", "reviewed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_batch_id_finding_id",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "batch_id", "finding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_batch_id_tenant_id_farm_id_survey_resu~",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_boundary_exception_id_batch_id_tenant_~",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "boundary_exception_id", "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_finding_id_tenant_id_farm_id_survey_re~",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "finding_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_previous_review_id_batch_id_kind_subje~",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "previous_review_id", "batch_id", "kind", "subject_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_reviews_zone_id_tenant_id_farm_id_survey_resul~",
                schema: "plant",
                table: "plant_health_reviews",
                columns: new[] { "zone_id", "tenant_id", "farm_id", "survey_result_id" });
            migrationBuilder.Sql("""
                CREATE FUNCTION plant.guard_health_review_history() RETURNS trigger
                LANGUAGE plpgsql AS $guard$
                BEGIN
                    RAISE EXCEPTION 'Plant Health review history and operation receipts are append-only.';
                END $guard$;
                CREATE TRIGGER trg_health_reviews_append_only
                    BEFORE UPDATE OR DELETE ON plant.plant_health_reviews
                    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_history();
                CREATE TRIGGER trg_health_review_operations_append_only
                    BEFORE UPDATE OR DELETE ON survey.plant_health_review_operations
                    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_history();
                CREATE FUNCTION plant.guard_health_review_zone_batch() RETURNS trigger
                LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF NEW.zone_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM plant.disease_zones z
                        WHERE z.id = NEW.zone_id AND z.import_batch_id = NEW.batch_id
                          AND z.tenant_id = NEW.tenant_id AND z.farm_id = NEW.farm_id
                          AND z.survey_result_id = NEW.survey_result_id AND z.source_schema_version = 4
                    ) THEN
                        RAISE EXCEPTION 'Zone review must belong to the source import batch.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER trg_health_review_zone_batch BEFORE INSERT ON plant.plant_health_reviews
                    FOR EACH ROW EXECUTE FUNCTION plant.guard_health_review_zone_batch();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM plant.plant_health_reviews)
                       OR EXISTS (SELECT 1 FROM plant.plant_health_review_states)
                       OR EXISTS (SELECT 1 FROM survey.plant_health_review_operations)
                       OR EXISTS (SELECT 1 FROM survey.survey_results WHERE review_revision > 0)
                       OR EXISTS (SELECT 1 FROM plant.disease_zones WHERE current_health_review_id IS NOT NULL)
                    THEN
                        RAISE EXCEPTION 'Step7B contains human review history. Reconcile/archive it explicitly or use roll-forward recovery.';
                    END IF;
                END $guard$;
                DROP TRIGGER IF EXISTS trg_health_reviews_append_only ON plant.plant_health_reviews;
                DROP TRIGGER IF EXISTS trg_health_review_zone_batch ON plant.plant_health_reviews;
                DROP TRIGGER IF EXISTS trg_health_review_operations_append_only ON survey.plant_health_review_operations;
                DROP FUNCTION IF EXISTS plant.guard_health_review_history();
                DROP FUNCTION IF EXISTS plant.guard_health_review_zone_batch();
                """);
            migrationBuilder.DropTable(
                name: "plant_health_review_operations",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "plant_health_review_states",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_health_reviews",
                schema: "plant");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_health_import_result_tenant",
                schema: "survey",
                table: "plant_health_import_batches");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_health_boundary_review_context",
                schema: "plant",
                table: "plant_health_boundary_exceptions");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_disease_zones_review_context",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "review_revision",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "review_updated_at",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "current_health_review_id",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "guidance_decision",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "reviewed_plant_condition_id",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "reviewed_suspected_class_code",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones",
                sql: "(status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)");
        }
    }
}
