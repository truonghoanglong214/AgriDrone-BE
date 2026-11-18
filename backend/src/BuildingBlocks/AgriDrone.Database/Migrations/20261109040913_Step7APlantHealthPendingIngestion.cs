using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step7APlantHealthPendingIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.AddColumn<Guid>(
                name: "active_import_batch_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_base_map_version_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_boundary_version_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "observed_at",
                schema: "survey",
                table: "survey_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_nature",
                schema: "survey",
                table: "survey_results",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_mission_id",
                schema: "survey",
                table: "survey_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "team_label_limitation",
                schema: "survey",
                table: "survey_results",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "health_level_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "guidance_suggestions",
                schema: "plant",
                table: "disease_zones",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_blocking_items",
                schema: "plant",
                table: "disease_zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "selected_sourced_guidance_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_schema_version",
                schema: "plant",
                table: "disease_zones",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "suspected_class_code",
                schema: "plant",
                table: "disease_zones",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "plant_id",
                schema: "plant",
                table: "disease_zone_memberships",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "mapping_candidate_id",
                schema: "plant",
                table: "disease_zone_memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_survey_results_id_tenant_farm",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "id", "tenant_id", "farm_id" });

            migrationBuilder.CreateTable(
                name: "plant_health_findings",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_version = table.Column<int>(type: "integer", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mapping_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_position = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    suspected_class_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    resolution_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    boundary_classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_findings", x => x.id);
                    table.UniqueConstraint("uq_health_finding_context", x => new { x.id, x.tenant_id, x.farm_id, x.survey_result_id });
                    table.CheckConstraint("ck_health_finding_point", "ST_IsValid(original_position) AND NOT ST_IsEmpty(original_position) AND ST_SRID(original_position) = 4326");
                    table.CheckConstraint("ck_health_finding_resolution", "resolution_state IN ('PENDING_REVIEW', 'NEEDS_REVIEW')");
                    table.CheckConstraint("ck_health_finding_version", "source_version > 0 AND length(fingerprint) = 64");
                    table.ForeignKey(
                        name: "fk_health_finding_plant",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_finding_result",
                        columns: x => new { x.survey_result_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_finding_supersedes",
                        columns: x => new { x.supersedes_finding_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_findings",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_health_import_batches",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_version = table.Column<int>(type: "integer", nullable: false),
                    supersedes_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    produced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    blocking_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_import_batches", x => x.id);
                    table.UniqueConstraint("uq_health_import_context", x => new { x.id, x.tenant_id, x.farm_id, x.survey_result_id });
                    table.CheckConstraint("ck_health_import_fingerprint", "length(fingerprint) = 64");
                    table.CheckConstraint("ck_health_import_source_version", "source_version > 0 AND blocking_count >= 0");
                    table.ForeignKey(
                        name: "fk_health_import_result_context",
                        columns: x => new { x.survey_result_id, x.survey_order_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "survey_order_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_import_result_tenant",
                        columns: x => new { x.survey_result_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_import_supersedes",
                        columns: x => new { x.supersedes_batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_health_batch_findings",
                schema: "plant",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_batch_findings", x => new { x.batch_id, x.finding_id });
                    table.ForeignKey(
                        name: "fk_health_batch_finding_batch",
                        columns: x => new { x.batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_health_batch_finding_context",
                        columns: x => new { x.finding_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "plant",
                        principalTable: "plant_health_findings",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plant_health_boundary_exceptions",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_position = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    original = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_health_boundary_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_health_boundary_exception_batch",
                        columns: x => new { x.batch_id, x.tenant_id, x.farm_id, x.survey_result_id },
                        principalSchema: "survey",
                        principalTable: "plant_health_import_batches",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_import_batch_id_tenant_id_farm_id_survey_resu~",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "import_batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_selected_sourced_guidance_id",
                schema: "plant",
                table: "disease_zones",
                column: "selected_sourced_guidance_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones",
                sql: "(status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND (current_membership_version = 1 OR source_schema_version = 4) AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zones_source_schema",
                schema: "plant",
                table: "disease_zones",
                sql: "(source_schema_version = 3 AND health_level_id IS NOT NULL AND import_batch_id IS NULL) OR (source_schema_version = 4 AND health_level_id IS NULL AND import_batch_id IS NOT NULL AND suspected_class_code IS NOT NULL AND selected_treatment_recommendation_id IS NULL AND guidance_suggestions IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "uq_disease_zone_memberships_candidate",
                schema: "plant",
                table: "disease_zone_memberships",
                columns: new[] { "disease_zone_id", "membership_version", "mapping_candidate_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zone_memberships_reference",
                schema: "plant",
                table: "disease_zone_memberships",
                sql: "plant_id IS NOT NULL OR mapping_candidate_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_batch_findings_batch_id_tenant_id_farm_id_surv~",
                schema: "plant",
                table: "plant_health_batch_findings",
                columns: new[] { "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_batch_findings_finding_id_tenant_id_farm_id_su~",
                schema: "plant",
                table: "plant_health_batch_findings",
                columns: new[] { "finding_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_boundary_exceptions_batch_id_tenant_id_farm_id~",
                schema: "plant",
                table: "plant_health_boundary_exceptions",
                columns: new[] { "batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "uq_health_boundary_exception_source",
                schema: "plant",
                table: "plant_health_boundary_exceptions",
                columns: new[] { "batch_id", "source_reference_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_finding_review_queue",
                schema: "plant",
                table: "plant_health_findings",
                columns: new[] { "survey_result_id", "resolution_state" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_findings_plant_id_farm_id",
                schema: "plant",
                table: "plant_health_findings",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_findings_supersedes_finding_id_tenant_id_farm_~",
                schema: "plant",
                table: "plant_health_findings",
                columns: new[] { "supersedes_finding_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_findings_survey_result_id_tenant_id_farm_id",
                schema: "plant",
                table: "plant_health_findings",
                columns: new[] { "survey_result_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_health_finding_source",
                schema: "plant",
                table: "plant_health_findings",
                columns: new[] { "tenant_id", "mission_id", "source_finding_id", "source_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_import_result_version",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "survey_result_id", "source_version" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_import_batches_supersedes_batch_id_tenant_id_f~",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "supersedes_batch_id", "tenant_id", "farm_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_import_batches_survey_result_id_survey_order_i~",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "survey_result_id", "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_health_import_batches_survey_result_id_tenant_id_farm~",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "survey_result_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_health_import_handoff",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "tenant_id", "handoff_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_health_import_job",
                schema: "survey",
                table: "plant_health_import_batches",
                columns: new[] { "tenant_id", "mission_id", "job_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_disease_zones_sourced_guidance",
                schema: "plant",
                table: "disease_zones",
                column: "selected_sourced_guidance_id",
                principalSchema: "plant",
                principalTable: "sourced_guidance",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_health_zone_batch",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "import_batch_id", "tenant_id", "farm_id", "survey_result_id" },
                principalSchema: "survey",
                principalTable: "plant_health_import_batches",
                principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_result_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A populated ingestion history must be reconciled/archived explicitly, never silently dropped.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.plant_health_import_batches)
                       OR EXISTS (SELECT 1 FROM survey.survey_results WHERE active_import_batch_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM plant.plant_health_findings)
                       OR EXISTS (SELECT 1 FROM plant.disease_zones WHERE source_schema_version = 4 OR health_level_id IS NULL)
                       OR EXISTS (SELECT 1 FROM plant.disease_zone_memberships WHERE plant_id IS NULL)
                    THEN
                        RAISE EXCEPTION 'Step7A contains pending/source history. Use roll-forward recovery or explicitly reconcile and archive it before schema rollback.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_disease_zones_sourced_guidance",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropForeignKey(
                name: "fk_health_zone_batch",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropTable(
                name: "plant_health_batch_findings",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_health_boundary_exceptions",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_health_findings",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_health_import_batches",
                schema: "survey");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_survey_results_id_tenant_farm",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropIndex(
                name: "IX_disease_zones_import_batch_id_tenant_id_farm_id_survey_resu~",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropIndex(
                name: "IX_disease_zones_selected_sourced_guidance_id",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zones_source_schema",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropIndex(
                name: "uq_disease_zone_memberships_candidate",
                schema: "plant",
                table: "disease_zone_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_disease_zone_memberships_reference",
                schema: "plant",
                table: "disease_zone_memberships");

            migrationBuilder.DropColumn(
                name: "active_import_batch_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "farm_base_map_version_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "farm_boundary_version_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "observed_at",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "result_nature",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "source_mission_id",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "team_label_limitation",
                schema: "survey",
                table: "survey_results");

            migrationBuilder.DropColumn(
                name: "guidance_suggestions",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "has_blocking_items",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "selected_sourced_guidance_id",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "source_schema_version",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "suspected_class_code",
                schema: "plant",
                table: "disease_zones");

            migrationBuilder.DropColumn(
                name: "mapping_candidate_id",
                schema: "plant",
                table: "disease_zone_memberships");

            migrationBuilder.AlterColumn<Guid>(
                name: "health_level_id",
                schema: "plant",
                table: "disease_zones",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "plant_id",
                schema: "plant",
                table: "disease_zone_memberships",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_disease_zones_lifecycle",
                schema: "plant",
                table: "disease_zones",
                sql: "(status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)");
        }
    }
}
