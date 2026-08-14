using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase4PlantConditionAiThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE plant.disease_lesions
                    DROP CONSTRAINT IF EXISTS fk_disease_lesions_detections_detection_id,
                    DROP CONSTRAINT IF EXISTS fk_disease_lesions_media_assets_media_id,
                    DROP CONSTRAINT IF EXISTS ck_lesion_affected_ratio,
                    DROP CONSTRAINT IF EXISTS ck_lesion_bbox_range,
                    DROP CONSTRAINT IF EXISTS ck_lesion_confidence;

                ALTER TABLE plant.disease_detections
                    DROP CONSTRAINT IF EXISTS fk_disease_detections_ai_models_model_version_id,
                    DROP CONSTRAINT IF EXISTS fk_disease_detections_diseases_disease_id,
                    DROP CONSTRAINT IF EXISTS fk_disease_detections_plant_scans_scan_id,
                    DROP CONSTRAINT IF EXISTS fk_disease_detections_users_reviewed_by,
                    DROP CONSTRAINT IF EXISTS ck_detection_affected_ratio,
                    DROP CONSTRAINT IF EXISTS ck_detection_confidence,
                    DROP CONSTRAINT IF EXISTS ck_detection_lesion_count;

                DROP INDEX IF EXISTS plant."IX_disease_lesions_disease_detection_id";
                DROP INDEX IF EXISTS plant."IX_disease_lesions_media_id";
                DROP INDEX IF EXISTS plant."IX_disease_detections_model_version_id";
                DROP INDEX IF EXISTS plant."IX_disease_detections_reviewed_by";
                DROP INDEX IF EXISTS plant.ix_disease_detections_disease;
                DROP INDEX IF EXISTS plant.ix_disease_detections_review;
                DROP INDEX IF EXISTS plant.ix_disease_detections_scan;
                DROP INDEX IF EXISTS plant.uq_detection_scan_disease;
                DROP INDEX IF EXISTS plant.uq_diseases_code;

                ALTER TABLE plant.diseases RENAME TO plant_conditions;
                ALTER TABLE plant.disease_detections RENAME TO condition_detections;
                ALTER TABLE plant.disease_lesions RENAME TO condition_lesions;
                ALTER TABLE plant.condition_detections RENAME COLUMN disease_id TO condition_id;
                ALTER TABLE plant.condition_lesions
                    RENAME COLUMN disease_detection_id TO condition_detection_id;

                ALTER TABLE plant.plant_conditions
                    RENAME CONSTRAINT pk_diseases TO pk_plant_conditions;
                ALTER TABLE plant.condition_detections
                    RENAME CONSTRAINT pk_disease_detections TO pk_condition_detections;
                ALTER TABLE plant.condition_lesions
                    RENAME CONSTRAINT pk_disease_lesions TO pk_condition_lesions;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_review_status", "PENDING,CONFIRMED,INCORRECT,FIELD_INSPECTION_REQUIRED")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .Annotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .Annotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_review_status", "PENDING,CONFIRMED,INCORRECT,FIELD_INSPECTION_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .OldAnnotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .OldAnnotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_health_level_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "overall_health_level_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_number",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "client_operation_id",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_code",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "input_manifest",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "model_version_id",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "output_manifest",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<decimal>(
                name: "progress_percent",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "threshold_profile_id",
                schema: "mission",
                table: "ai_processing_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_threshold_profiles",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "system.threshold_profile_status", nullable: false, defaultValueSql: "'DRAFT'::system.threshold_profile_status"),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_threshold_profiles", x => x.id);
                    table.UniqueConstraint("uq_ai_threshold_profiles_id_model", x => new { x.id, x.model_version_id });
                    table.CheckConstraint("ck_ai_threshold_profile_effective_time", "effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_ai_threshold_profile_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_ai_threshold_profiles_model_versions_model_id",
                        column: x => x.model_version_id,
                        principalSchema: "mission",
                        principalTable: "ai_model_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_threshold_profiles_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Versioned threshold profile for one AI model; active versions are immutable.");

            migrationBuilder.CreateTable(
                name: "health_levels",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    is_healthy = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_levels", x => x.id);
                    table.CheckConstraint("ck_health_levels_semantics", "(code = 'UNKNOWN' AND rank IS NULL AND is_healthy = FALSE) OR (code = 'HEALTHY' AND rank = 0 AND is_healthy = TRUE) OR (code NOT IN ('UNKNOWN', 'HEALTHY') AND rank > 0 AND is_healthy = FALSE)");
                },
                comment: "Global ordered health/severity levels shared by plants, scans and condition detections.");

            migrationBuilder.AddColumn<int>(
                name: "condition_type",
                schema: "plant",
                table: "plant_conditions",
                type: "system.condition_type",
                nullable: false,
                defaultValueSql: "'DISEASE'::system.condition_type");

            migrationBuilder.AlterTable(
                name: "plant_conditions",
                schema: "plant",
                comment: "Global catalog of diseases, abiotic damage and mechanical plant conditions.",
                oldComment: "Configurable disease catalog. Disease types are data, not hard-coded columns.");

            migrationBuilder.CreateIndex(
                name: "uq_plant_conditions_code",
                schema: "plant",
                table: "plant_conditions",
                column: "code",
                unique: true);

            migrationBuilder.CreateTable(
                name: "ai_detection_thresholds",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    threshold_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_detection_thresholds", x => x.id);
                    table.CheckConstraint("ck_ai_detection_threshold_confidence", "min_confidence BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_ai_detection_thresholds_conditions_condition_id",
                        column: x => x.condition_id,
                        principalSchema: "plant",
                        principalTable: "plant_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ai_detection_thresholds_profiles_profile_id",
                        column: x => x.threshold_profile_id,
                        principalSchema: "mission",
                        principalTable: "ai_threshold_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Per-condition confidence thresholds belonging to a versioned AI threshold profile.");

            migrationBuilder.AddColumn<Guid>(
                name: "severity_level_id",
                schema: "plant",
                table: "condition_detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "threshold_used",
                schema: "plant",
                table: "condition_detections",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "plant",
                table: "condition_detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterTable(
                name: "condition_detections",
                schema: "plant",
                comment: "Immutable AI/manual condition findings for a plant scan with reproducible threshold metadata.",
                oldComment: "Disease findings for a plant scan, including confidence, severity, AI model and review state.");

            migrationBuilder.Sql(
                """
                INSERT INTO plant.health_levels
                    (id, code, name, rank, is_healthy, description, is_active, created_at, updated_at)
                VALUES
                    ('11111111-1111-4111-8111-111111111101', 'UNKNOWN', 'Unknown', NULL, FALSE,
                     'Health has not been assessed.', TRUE, NOW(), NOW()),
                    ('11111111-1111-4111-8111-111111111102', 'HEALTHY', 'Healthy', 0, TRUE,
                     'No material health condition detected.', TRUE, NOW(), NOW()),
                    ('11111111-1111-4111-8111-111111111103', 'MILD', 'Mild', 1, FALSE,
                     'Low-severity condition.', TRUE, NOW(), NOW()),
                    ('11111111-1111-4111-8111-111111111104', 'MODERATE', 'Moderate', 2, FALSE,
                     'Moderate-severity condition.', TRUE, NOW(), NOW()),
                    ('11111111-1111-4111-8111-111111111105', 'SEVERE', 'Severe', 3, FALSE,
                     'High-severity condition.', TRUE, NOW(), NOW());

                INSERT INTO plant.plant_conditions
                    (id, code, name, scientific_name, condition_type, description,
                     is_active, created_at, updated_at)
                VALUES
                    (gen_random_uuid(), 'BROWN_SPOT', 'Brown Spot', NULL,
                     'DISEASE'::system.condition_type, NULL, TRUE, NOW(), NOW()),
                    (gen_random_uuid(), 'ANTHRACNOSE', 'Anthracnose', NULL,
                     'DISEASE'::system.condition_type, NULL, TRUE, NOW(), NOW()),
                    (gen_random_uuid(), 'SUNBURN', 'Sunburn', NULL,
                     'ABIOTIC_DAMAGE'::system.condition_type, NULL, TRUE, NOW(), NOW()),
                    (gen_random_uuid(), 'MECHANICAL_SCAR', 'Mechanical Scar', NULL,
                     'MECHANICAL_DAMAGE'::system.condition_type, NULL, TRUE, NOW(), NOW())
                ON CONFLICT (code) DO UPDATE
                SET condition_type = EXCLUDED.condition_type;

                UPDATE plant.plants AS plant
                SET current_health_level_id = level.id
                FROM plant.health_levels AS level
                WHERE level.code = plant.current_health_status::text;

                UPDATE plant.plant_scans AS scan
                SET overall_health_level_id = level.id
                FROM plant.health_levels AS level
                WHERE level.code = scan.overall_health_status::text;

                UPDATE plant.scan_verifications AS verification
                SET corrected_health_level_id = level.id
                FROM plant.health_levels AS level
                WHERE verification.corrected_health_status IS NOT NULL
                  AND level.code = verification.corrected_health_status::text;

                UPDATE plant.condition_detections AS detection
                SET severity_level_id = level.id
                FROM plant.health_levels AS level
                WHERE level.code = detection.severity::text;

                UPDATE plant.condition_detections AS detection
                SET created_by = COALESCE(detection.reviewed_by, scan.created_by)
                FROM plant.plant_scans AS scan
                WHERE scan.id = detection.plant_scan_id
                  AND detection.source = 'MANUAL'::system.finding_source
                  AND detection.created_by IS NULL;

                DO $phase4$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM plant.condition_detections
                        WHERE source = 'MANUAL'::system.finding_source
                          AND created_by IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 4 backfill failed: a manual condition detection has no resolvable creator.';
                    END IF;
                END
                $phase4$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_detection_affected_ratio",
                schema: "plant",
                table: "condition_detections",
                sql: "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_detection_confidence",
                schema: "plant",
                table: "condition_detections",
                sql: "confidence IS NULL OR confidence BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_detection_lesion_count",
                schema: "plant",
                table: "condition_detections",
                sql: "lesion_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_detection_manual_creator",
                schema: "plant",
                table: "condition_detections",
                sql: "source <> 'MANUAL'::system.finding_source OR created_by IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_detection_threshold",
                schema: "plant",
                table: "condition_detections",
                sql: "threshold_used IS NULL OR threshold_used BETWEEN 0 AND 1");

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_ai_models_model_version_id",
                schema: "plant",
                table: "condition_detections",
                column: "model_version_id",
                principalSchema: "mission",
                principalTable: "ai_model_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_conditions_condition_id",
                schema: "plant",
                table: "condition_detections",
                column: "condition_id",
                principalSchema: "plant",
                principalTable: "plant_conditions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_health_levels_severity_level_id",
                schema: "plant",
                table: "condition_detections",
                column: "severity_level_id",
                principalSchema: "plant",
                principalTable: "health_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_plant_scans_scan_id",
                schema: "plant",
                table: "condition_detections",
                column: "plant_scan_id",
                principalSchema: "plant",
                principalTable: "plant_scans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_users_created_by",
                schema: "plant",
                table: "condition_detections",
                column: "created_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_users_reviewed_by",
                schema: "plant",
                table: "condition_detections",
                column: "reviewed_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AlterTable(
                name: "condition_lesions",
                schema: "plant",
                comment: "Individual condition bounding boxes/localized affected areas on an image.",
                oldComment: "Individual disease bounding boxes/localized affected areas on an image.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_lesion_affected_ratio",
                schema: "plant",
                table: "condition_lesions",
                sql: "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_lesion_bbox_range",
                schema: "plant",
                table: "condition_lesions",
                sql: "x_min BETWEEN 0 AND 1 AND y_min BETWEEN 0 AND 1 AND x_max BETWEEN 0 AND 1 AND y_max BETWEEN 0 AND 1 AND x_min < x_max AND y_min < y_max");

            migrationBuilder.AddCheckConstraint(
                name: "ck_condition_lesion_confidence",
                schema: "plant",
                table: "condition_lesions",
                sql: "confidence IS NULL OR confidence BETWEEN 0 AND 1");

            migrationBuilder.AddForeignKey(
                name: "fk_condition_lesions_detections_detection_id",
                schema: "plant",
                table: "condition_lesions",
                column: "condition_detection_id",
                principalSchema: "plant",
                principalTable: "condition_detections",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_condition_lesions_media_assets_media_id",
                schema: "plant",
                table: "condition_lesions",
                column: "media_id",
                principalSchema: "media",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_scan_verifications_corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications",
                column: "corrected_health_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_plants_current_health_level_id",
                schema: "plant",
                table: "plants",
                column: "current_health_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_zone_health_level",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "current_health_level_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_farm_health_level",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "farm_id", "overall_health_level_id", "observed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_overall_health_level_id",
                schema: "plant",
                table: "plant_scans",
                column: "overall_health_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_processing_jobs_model_version_id",
                schema: "mission",
                table: "ai_processing_jobs",
                column: "model_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_processing_jobs_threshold_profile_id_model_version_id",
                schema: "mission",
                table: "ai_processing_jobs",
                columns: new[] { "threshold_profile_id", "model_version_id" });

            migrationBuilder.CreateIndex(
                name: "uq_ai_jobs_client_operation",
                schema: "mission",
                table: "ai_processing_jobs",
                column: "client_operation_id",
                unique: true,
                filter: "client_operation_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_job_attempt",
                schema: "mission",
                table: "ai_processing_jobs",
                sql: "attempt_number >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_job_progress",
                schema: "mission",
                table: "ai_processing_jobs",
                sql: "progress_percent IS NULL OR progress_percent BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_job_threshold_model",
                schema: "mission",
                table: "ai_processing_jobs",
                sql: "threshold_profile_id IS NULL OR model_version_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ai_detection_thresholds_condition_id",
                schema: "mission",
                table: "ai_detection_thresholds",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "uq_ai_detection_thresholds_profile_condition",
                schema: "mission",
                table: "ai_detection_thresholds",
                columns: new[] { "threshold_profile_id", "condition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_threshold_profiles_created_by",
                schema: "mission",
                table: "ai_threshold_profiles",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "uq_ai_threshold_profiles_model_name_version",
                schema: "mission",
                table: "ai_threshold_profiles",
                columns: new[] { "model_version_id", "profile_name", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_condition_detections_condition",
                schema: "plant",
                table: "condition_detections",
                column: "condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detections_created_by",
                schema: "plant",
                table: "condition_detections",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detections_model_version_id",
                schema: "plant",
                table: "condition_detections",
                column: "model_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_condition_detections_review",
                schema: "plant",
                table: "condition_detections",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detections_reviewed_by",
                schema: "plant",
                table: "condition_detections",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_condition_detections_scan",
                schema: "plant",
                table: "condition_detections",
                column: "plant_scan_id");

            migrationBuilder.CreateIndex(
                name: "ix_condition_detections_severity_level",
                schema: "plant",
                table: "condition_detections",
                column: "severity_level_id");

            migrationBuilder.CreateIndex(
                name: "uq_condition_detection_scan_condition",
                schema: "plant",
                table: "condition_detections",
                columns: new[] { "plant_scan_id", "condition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_condition_lesions_condition_detection_id",
                schema: "plant",
                table: "condition_lesions",
                column: "condition_detection_id");

            migrationBuilder.CreateIndex(
                name: "IX_condition_lesions_media_id",
                schema: "plant",
                table: "condition_lesions",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "uq_health_levels_code",
                schema: "plant",
                table: "health_levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_health_levels_rank",
                schema: "plant",
                table: "health_levels",
                column: "rank",
                unique: true,
                filter: "rank IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_processing_jobs_model_versions_model_id",
                schema: "mission",
                table: "ai_processing_jobs",
                column: "model_version_id",
                principalSchema: "mission",
                principalTable: "ai_model_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_processing_jobs_threshold_profile_same_model",
                schema: "mission",
                table: "ai_processing_jobs",
                columns: new[] { "threshold_profile_id", "model_version_id" },
                principalSchema: "mission",
                principalTable: "ai_threshold_profiles",
                principalColumns: new[] { "id", "model_version_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_health_levels_overall_health_level_id",
                schema: "plant",
                table: "plant_scans",
                column: "overall_health_level_id",
                principalSchema: "plant",
                principalTable: "health_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plants_health_levels_current_health_level_id",
                schema: "plant",
                table: "plants",
                column: "current_health_level_id",
                principalSchema: "plant",
                principalTable: "health_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_scan_verifications_health_levels_corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications",
                column: "corrected_health_level_id",
                principalSchema: "plant",
                principalTable: "health_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.prevent_master_code_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.code IS DISTINCT FROM OLD.code THEN
                        RAISE EXCEPTION 'Master-data code is immutable once created.';
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_plant_conditions_immutable_code
                BEFORE UPDATE ON plant.plant_conditions
                FOR EACH ROW EXECUTE FUNCTION plant.prevent_master_code_change();

                CREATE TRIGGER trg_health_levels_immutable_code
                BEFORE UPDATE ON plant.health_levels
                FOR EACH ROW EXECUTE FUNCTION plant.prevent_master_code_change();

                CREATE OR REPLACE FUNCTION mission.protect_active_threshold_profile()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF OLD.status = 'ACTIVE'::system.threshold_profile_status THEN
                        IF TG_OP = 'DELETE' THEN
                            RAISE EXCEPTION 'An active threshold profile cannot be deleted.';
                        END IF;

                        IF NOT (
                            NEW.status = 'RETIRED'::system.threshold_profile_status
                            AND (to_jsonb(NEW) - 'status' - 'effective_to') =
                                (to_jsonb(OLD) - 'status' - 'effective_to')
                        ) THEN
                            RAISE EXCEPTION
                                'An active threshold profile is immutable; create a new version instead.';
                        END IF;
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_ai_threshold_profiles_protect_active
                BEFORE UPDATE OR DELETE ON mission.ai_threshold_profiles
                FOR EACH ROW EXECUTE FUNCTION mission.protect_active_threshold_profile();

                CREATE OR REPLACE FUNCTION mission.protect_active_detection_threshold()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    profile_id uuid;
                BEGIN
                    profile_id := CASE WHEN TG_OP = 'DELETE'
                        THEN OLD.threshold_profile_id
                        ELSE NEW.threshold_profile_id
                    END;

                    IF EXISTS (
                        SELECT 1
                        FROM mission.ai_threshold_profiles
                        WHERE id = profile_id
                          AND status = 'ACTIVE'::system.threshold_profile_status
                    ) THEN
                        RAISE EXCEPTION
                            'Thresholds in an active profile are immutable; create a new profile version.';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_ai_detection_thresholds_protect_active
                BEFORE INSERT OR UPDATE OR DELETE ON mission.ai_detection_thresholds
                FOR EACH ROW EXECUTE FUNCTION mission.protect_active_detection_threshold();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_ai_detection_thresholds_protect_active
                    ON mission.ai_detection_thresholds;
                DROP TRIGGER IF EXISTS trg_ai_threshold_profiles_protect_active
                    ON mission.ai_threshold_profiles;
                DROP TRIGGER IF EXISTS trg_plant_conditions_immutable_code
                    ON plant.plant_conditions;
                DROP TRIGGER IF EXISTS trg_health_levels_immutable_code
                    ON plant.health_levels;
                DROP FUNCTION IF EXISTS mission.protect_active_detection_threshold();
                DROP FUNCTION IF EXISTS mission.protect_active_threshold_profile();
                DROP FUNCTION IF EXISTS plant.prevent_master_code_change();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_ai_processing_jobs_model_versions_model_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_ai_processing_jobs_threshold_profile_same_model",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_health_levels_overall_health_level_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropForeignKey(
                name: "fk_plants_health_levels_current_health_level_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropForeignKey(
                name: "fk_scan_verifications_health_levels_corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropTable(
                name: "ai_detection_thresholds",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "ai_threshold_profiles",
                schema: "mission");

            migrationBuilder.Sql(
                """
                ALTER TABLE plant.condition_lesions
                    DROP CONSTRAINT IF EXISTS fk_condition_lesions_detections_detection_id,
                    DROP CONSTRAINT IF EXISTS fk_condition_lesions_media_assets_media_id,
                    DROP CONSTRAINT IF EXISTS ck_condition_lesion_affected_ratio,
                    DROP CONSTRAINT IF EXISTS ck_condition_lesion_bbox_range,
                    DROP CONSTRAINT IF EXISTS ck_condition_lesion_confidence;

                ALTER TABLE plant.condition_detections
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_ai_models_model_version_id,
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_conditions_condition_id,
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_health_levels_severity_level_id,
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_plant_scans_scan_id,
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_users_created_by,
                    DROP CONSTRAINT IF EXISTS fk_condition_detections_users_reviewed_by,
                    DROP CONSTRAINT IF EXISTS ck_condition_detection_affected_ratio,
                    DROP CONSTRAINT IF EXISTS ck_condition_detection_confidence,
                    DROP CONSTRAINT IF EXISTS ck_condition_detection_lesion_count,
                    DROP CONSTRAINT IF EXISTS ck_condition_detection_manual_creator,
                    DROP CONSTRAINT IF EXISTS ck_condition_detection_threshold;

                DROP INDEX IF EXISTS plant."IX_condition_lesions_condition_detection_id";
                DROP INDEX IF EXISTS plant."IX_condition_lesions_media_id";
                DROP INDEX IF EXISTS plant."IX_condition_detections_created_by";
                DROP INDEX IF EXISTS plant."IX_condition_detections_model_version_id";
                DROP INDEX IF EXISTS plant."IX_condition_detections_reviewed_by";
                DROP INDEX IF EXISTS plant.ix_condition_detections_condition;
                DROP INDEX IF EXISTS plant.ix_condition_detections_review;
                DROP INDEX IF EXISTS plant.ix_condition_detections_scan;
                DROP INDEX IF EXISTS plant.ix_condition_detections_severity_level;
                DROP INDEX IF EXISTS plant.uq_condition_detection_scan_condition;
                DROP INDEX IF EXISTS plant.uq_plant_conditions_code;

                ALTER TABLE plant.condition_detections
                    DROP COLUMN severity_level_id,
                    DROP COLUMN threshold_used,
                    DROP COLUMN created_by;
                ALTER TABLE plant.plant_conditions DROP COLUMN condition_type;

                ALTER TABLE plant.condition_detections RENAME COLUMN condition_id TO disease_id;
                ALTER TABLE plant.condition_lesions
                    RENAME COLUMN condition_detection_id TO disease_detection_id;
                ALTER TABLE plant.plant_conditions RENAME TO diseases;
                ALTER TABLE plant.condition_detections RENAME TO disease_detections;
                ALTER TABLE plant.condition_lesions RENAME TO disease_lesions;

                ALTER TABLE plant.diseases
                    RENAME CONSTRAINT pk_plant_conditions TO pk_diseases;
                ALTER TABLE plant.disease_detections
                    RENAME CONSTRAINT pk_condition_detections TO pk_disease_detections;
                ALTER TABLE plant.disease_lesions
                    RENAME CONSTRAINT pk_condition_lesions TO pk_disease_lesions;

                COMMENT ON TABLE plant.diseases IS
                    'Configurable disease catalog. Disease types are data, not hard-coded columns.';
                COMMENT ON TABLE plant.disease_detections IS
                    'Disease findings for a plant scan, including confidence, severity, AI model and review state.';
                COMMENT ON TABLE plant.disease_lesions IS
                    'Individual disease bounding boxes/localized affected areas on an image.';

                ALTER TABLE plant.disease_detections
                    ADD CONSTRAINT ck_detection_affected_ratio
                        CHECK (affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1),
                    ADD CONSTRAINT ck_detection_confidence
                        CHECK (confidence IS NULL OR confidence BETWEEN 0 AND 1),
                    ADD CONSTRAINT ck_detection_lesion_count CHECK (lesion_count >= 0),
                    ADD CONSTRAINT fk_disease_detections_ai_models_model_version_id
                        FOREIGN KEY (model_version_id) REFERENCES mission.ai_model_versions(id)
                        ON DELETE SET NULL,
                    ADD CONSTRAINT fk_disease_detections_diseases_disease_id
                        FOREIGN KEY (disease_id) REFERENCES plant.diseases(id) ON DELETE RESTRICT,
                    ADD CONSTRAINT fk_disease_detections_plant_scans_scan_id
                        FOREIGN KEY (plant_scan_id) REFERENCES plant.plant_scans(id) ON DELETE CASCADE,
                    ADD CONSTRAINT fk_disease_detections_users_reviewed_by
                        FOREIGN KEY (reviewed_by) REFERENCES identity.users(id) ON DELETE SET NULL;

                ALTER TABLE plant.disease_lesions
                    ADD CONSTRAINT ck_lesion_affected_ratio
                        CHECK (affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1),
                    ADD CONSTRAINT ck_lesion_bbox_range
                        CHECK (x_min BETWEEN 0 AND 1 AND y_min BETWEEN 0 AND 1 AND
                               x_max BETWEEN 0 AND 1 AND y_max BETWEEN 0 AND 1 AND
                               x_min < x_max AND y_min < y_max),
                    ADD CONSTRAINT ck_lesion_confidence
                        CHECK (confidence IS NULL OR confidence BETWEEN 0 AND 1),
                    ADD CONSTRAINT fk_disease_lesions_detections_detection_id
                        FOREIGN KEY (disease_detection_id)
                        REFERENCES plant.disease_detections(id) ON DELETE CASCADE,
                    ADD CONSTRAINT fk_disease_lesions_media_assets_media_id
                        FOREIGN KEY (media_id) REFERENCES media.media_assets(id) ON DELETE RESTRICT;

                CREATE UNIQUE INDEX uq_diseases_code ON plant.diseases(code);
                CREATE UNIQUE INDEX uq_detection_scan_disease
                    ON plant.disease_detections(plant_scan_id, disease_id);
                CREATE INDEX ix_disease_detections_disease
                    ON plant.disease_detections(disease_id);
                CREATE INDEX ix_disease_detections_review
                    ON plant.disease_detections(review_status);
                CREATE INDEX ix_disease_detections_scan
                    ON plant.disease_detections(plant_scan_id);
                CREATE INDEX "IX_disease_detections_model_version_id"
                    ON plant.disease_detections(model_version_id);
                CREATE INDEX "IX_disease_detections_reviewed_by"
                    ON plant.disease_detections(reviewed_by);
                CREATE INDEX "IX_disease_lesions_disease_detection_id"
                    ON plant.disease_lesions(disease_detection_id);
                CREATE INDEX "IX_disease_lesions_media_id"
                    ON plant.disease_lesions(media_id);
                """);

            migrationBuilder.DropTable(
                name: "health_levels",
                schema: "plant");

            migrationBuilder.DropIndex(
                name: "IX_scan_verifications_corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropIndex(
                name: "IX_plants_current_health_level_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "ix_plants_zone_health_level",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "ix_plant_scans_farm_health_level",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_overall_health_level_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_ai_processing_jobs_model_version_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_ai_processing_jobs_threshold_profile_id_model_version_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropIndex(
                name: "uq_ai_jobs_client_operation",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_job_attempt",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_job_progress",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_job_threshold_model",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "corrected_health_level_id",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropColumn(
                name: "current_health_level_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "overall_health_level_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "attempt_number",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "client_operation_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "error_code",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "input_manifest",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "model_version_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "output_manifest",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "progress_percent",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.DropColumn(
                name: "threshold_profile_id",
                schema: "mission",
                table: "ai_processing_jobs");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_review_status", "PENDING,CONFIRMED,INCORRECT,FIELD_INSPECTION_REQUIRED")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .Annotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .Annotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_review_status", "PENDING,CONFIRMED,INCORRECT,FIELD_INSPECTION_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .OldAnnotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .OldAnnotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

        }
    }
}
