using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase5HumanVerificationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE system.verification_decision
                    ADD VALUE IF NOT EXISTS 'CORRECTED';
                ALTER TYPE system.verification_decision
                    ADD VALUE IF NOT EXISTS 'REJECTED';
                ALTER TYPE system.verification_decision
                    ADD VALUE IF NOT EXISTS 'FIELD_INSPECTION_REQUIRED';
                """,
                suppressTransaction: true);

            migrationBuilder.DropForeignKey(
                name: "fk_task_updates_field_tasks_task_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
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

            migrationBuilder.AlterTable(
                name: "scan_verifications",
                schema: "plant",
                comment: "Immutable revisioned review sessions for manager/worker verification of scan results.",
                oldComment: "Immutable verification history for manager/worker confirmation or rejection of scan results.");

            migrationBuilder.AddColumn<Guid>(
                name: "client_operation_id",
                schema: "field_task",
                table: "task_updates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_scan_id",
                schema: "field_task",
                table: "task_updates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "device_created_at",
                schema: "field_task",
                table: "task_updates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_id",
                schema: "field_task",
                table: "task_updates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "server_received_at",
                schema: "field_task",
                table: "task_updates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<int>(
                name: "revision_number",
                schema: "plant",
                table: "scan_verifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_verification_id",
                schema: "plant",
                table: "scan_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "client_operation_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "device_created_at",
                schema: "plant",
                table: "plant_scans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "server_received_at",
                schema: "plant",
                table: "plant_scans",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<Guid>(
                name: "source_task_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verification_of_scan_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE field_task.task_updates AS update
                SET farm_id = task.farm_id,
                    server_received_at = update.created_at
                FROM field_task.field_tasks AS task
                WHERE task.id = update.task_id;

                UPDATE plant.plant_scans
                SET server_received_at = created_at;

                WITH ordered_verifications AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY plant_scan_id
                            ORDER BY created_at, id
                        ) AS revision_number,
                        LAG(id) OVER (
                            PARTITION BY plant_scan_id
                            ORDER BY created_at, id
                        ) AS supersedes_verification_id
                    FROM plant.scan_verifications
                )
                UPDATE plant.scan_verifications AS verification
                SET revision_number = ordered.revision_number,
                    supersedes_verification_id = ordered.supersedes_verification_id
                FROM ordered_verifications AS ordered
                WHERE ordered.id = verification.id;

                UPDATE plant.scan_verifications
                SET decision = CASE
                    WHEN decision = 'INCORRECT'::system.verification_decision
                         AND (corrected_health_level_id IS NOT NULL OR corrected_health_status IS NOT NULL)
                        THEN 'CORRECTED'::system.verification_decision
                    WHEN decision = 'INCORRECT'::system.verification_decision
                        THEN 'REJECTED'::system.verification_decision
                    WHEN decision = 'RECOVERED'::system.verification_decision
                        THEN 'CORRECTED'::system.verification_decision
                    WHEN decision = 'NEED_FIELD_INSPECTION'::system.verification_decision
                        THEN 'FIELD_INSPECTION_REQUIRED'::system.verification_decision
                    ELSE decision
                END;

                DO $phase5$
                BEGIN
                    IF EXISTS (SELECT 1 FROM field_task.task_updates WHERE farm_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Phase 5 backfill failed: a task update has no resolvable farm.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.scan_verifications
                        WHERE revision_number IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 5 backfill failed: a scan verification has no revision number.';
                    END IF;
                END
                $phase5$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "farm_id",
                schema: "field_task",
                table: "task_updates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "revision_number",
                schema: "plant",
                table: "scan_verifications",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_scan_verifications_id_scan",
                schema: "plant",
                table: "scan_verifications",
                columns: new[] { "id", "plant_scan_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_plant_scans_id_plant_farm",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "id", "plant_id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_field_tasks_id_farm",
                schema: "field_task",
                table: "field_tasks",
                columns: new[] { "id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_condition_detections_id_scan",
                schema: "plant",
                table: "condition_detections",
                columns: new[] { "id", "plant_scan_id" });

            migrationBuilder.CreateTable(
                name: "condition_detection_reviews",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    scan_verification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_scan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition_detection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<int>(type: "system.condition_review_decision", nullable: false),
                    corrected_condition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corrected_severity_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condition_detection_reviews", x => x.id);
                    table.CheckConstraint("ck_condition_review_correction_values", "(decision = 'CORRECTED'::system.condition_review_decision AND (corrected_condition_id IS NOT NULL OR corrected_severity_level_id IS NOT NULL)) OR (decision IN ('CONFIRMED'::system.condition_review_decision, 'REJECTED'::system.condition_review_decision) AND corrected_condition_id IS NULL AND corrected_severity_level_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_condition_reviews_corrected_condition_id",
                        column: x => x.corrected_condition_id,
                        principalSchema: "plant",
                        principalTable: "plant_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_condition_reviews_corrected_severity_level_id",
                        column: x => x.corrected_severity_level_id,
                        principalSchema: "plant",
                        principalTable: "health_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_condition_reviews_detection_same_scan",
                        columns: x => new { x.condition_detection_id, x.plant_scan_id },
                        principalSchema: "plant",
                        principalTable: "condition_detections",
                        principalColumns: new[] { "id", "plant_scan_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_condition_reviews_verification_same_scan",
                        columns: x => new { x.scan_verification_id, x.plant_scan_id },
                        principalSchema: "plant",
                        principalTable: "scan_verifications",
                        principalColumns: new[] { "id", "plant_scan_id" },
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Immutable human review items that preserve the original condition prediction.");

            migrationBuilder.CreateIndex(
                name: "IX_task_updates_created_scan_id_farm_id",
                schema: "field_task",
                table: "task_updates",
                columns: new[] { "created_scan_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_updates_task_id_farm_id",
                schema: "field_task",
                table: "task_updates",
                columns: new[] { "task_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_task_updates_client_operation",
                schema: "field_task",
                table: "task_updates",
                column: "client_operation_id",
                unique: true,
                filter: "client_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_scan_verifications_supersedes_verification_id_plant_scan_id",
                schema: "plant",
                table: "scan_verifications",
                columns: new[] { "supersedes_verification_id", "plant_scan_id" });

            migrationBuilder.CreateIndex(
                name: "uq_scan_verifications_scan_revision",
                schema: "plant",
                table: "scan_verifications",
                columns: new[] { "plant_scan_id", "revision_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_scan_verification_revision_chain",
                schema: "plant",
                table: "scan_verifications",
                sql: "(revision_number = 1 AND supersedes_verification_id IS NULL) OR (revision_number > 1 AND supersedes_verification_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_scan_verification_revision_positive",
                schema: "plant",
                table: "scan_verifications",
                sql: "revision_number >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_scan_verification_target_decision",
                schema: "plant",
                table: "scan_verifications",
                sql: "decision IN ('CONFIRMED'::system.verification_decision, 'CORRECTED'::system.verification_decision, 'REJECTED'::system.verification_decision, 'FIELD_INSPECTION_REQUIRED'::system.verification_decision)");

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_source_task_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "source_task_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_verification_of_scan_id_plant_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "verification_of_scan_id", "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_plant_scans_client_operation",
                schema: "plant",
                table: "plant_scans",
                column: "client_operation_id",
                unique: true,
                filter: "client_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detection_reviews_condition_detection_id_plant_sc~",
                schema: "plant",
                table: "condition_detection_reviews",
                columns: new[] { "condition_detection_id", "plant_scan_id" });

            migrationBuilder.CreateIndex(
                name: "IX_condition_detection_reviews_corrected_condition_id",
                schema: "plant",
                table: "condition_detection_reviews",
                column: "corrected_condition_id");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detection_reviews_corrected_severity_level_id",
                schema: "plant",
                table: "condition_detection_reviews",
                column: "corrected_severity_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_condition_detection_reviews_scan_verification_id_plant_scan~",
                schema: "plant",
                table: "condition_detection_reviews",
                columns: new[] { "scan_verification_id", "plant_scan_id" });

            migrationBuilder.CreateIndex(
                name: "uq_condition_reviews_verification_detection",
                schema: "plant",
                table: "condition_detection_reviews",
                columns: new[] { "scan_verification_id", "condition_detection_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_source_task_same_farm",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "source_task_id", "farm_id" },
                principalSchema: "field_task",
                principalTable: "field_tasks",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_verification_of_same_plant_farm",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "verification_of_scan_id", "plant_id", "farm_id" },
                principalSchema: "plant",
                principalTable: "plant_scans",
                principalColumns: new[] { "id", "plant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_scan_verifications_supersedes_same_scan",
                schema: "plant",
                table: "scan_verifications",
                columns: new[] { "supersedes_verification_id", "plant_scan_id" },
                principalSchema: "plant",
                principalTable: "scan_verifications",
                principalColumns: new[] { "id", "plant_scan_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_task_updates_created_scan_same_farm",
                schema: "field_task",
                table: "task_updates",
                columns: new[] { "created_scan_id", "farm_id" },
                principalSchema: "plant",
                principalTable: "plant_scans",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_task_updates_field_tasks_same_farm",
                schema: "field_task",
                table: "task_updates",
                columns: new[] { "task_id", "farm_id" },
                principalSchema: "field_task",
                principalTable: "field_tasks",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.reject_immutable_review_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION '% records are immutable; create a new revision instead.', TG_TABLE_NAME;
                END
                $function$;

                CREATE TRIGGER trg_condition_detections_immutable
                BEFORE UPDATE OR DELETE ON plant.condition_detections
                FOR EACH ROW EXECUTE FUNCTION plant.reject_immutable_review_mutation();

                CREATE TRIGGER trg_scan_verifications_immutable
                BEFORE UPDATE OR DELETE ON plant.scan_verifications
                FOR EACH ROW EXECUTE FUNCTION plant.reject_immutable_review_mutation();

                CREATE TRIGGER trg_condition_detection_reviews_immutable
                BEFORE UPDATE OR DELETE ON plant.condition_detection_reviews
                FOR EACH ROW EXECUTE FUNCTION plant.reject_immutable_review_mutation();

                CREATE OR REPLACE FUNCTION plant.validate_scan_verification_revision()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    previous_revision integer;
                BEGIN
                    IF NEW.revision_number = 1 THEN
                        RETURN NEW;
                    END IF;

                    SELECT revision_number
                    INTO previous_revision
                    FROM plant.scan_verifications
                    WHERE id = NEW.supersedes_verification_id
                      AND plant_scan_id = NEW.plant_scan_id;

                    IF previous_revision IS NULL OR previous_revision <> NEW.revision_number - 1 THEN
                        RAISE EXCEPTION
                            'A scan verification must supersede the immediately preceding revision.';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_scan_verifications_validate_revision
                BEFORE INSERT ON plant.scan_verifications
                FOR EACH ROW EXECUTE FUNCTION plant.validate_scan_verification_revision();

                CREATE OR REPLACE FUNCTION plant.validate_corrected_scan_verification()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    verification_id uuid;
                    verification_decision system.verification_decision;
                    corrected_health_id uuid;
                    has_corrected_item boolean;
                BEGIN
                    verification_id := CASE
                        WHEN TG_TABLE_NAME = 'scan_verifications' THEN NEW.id
                        ELSE NEW.scan_verification_id
                    END;

                    SELECT decision, corrected_health_level_id
                    INTO verification_decision, corrected_health_id
                    FROM plant.scan_verifications
                    WHERE id = verification_id;

                    SELECT EXISTS (
                        SELECT 1
                        FROM plant.condition_detection_reviews
                        WHERE scan_verification_id = verification_id
                          AND decision = 'CORRECTED'::system.condition_review_decision
                    ) INTO has_corrected_item;

                    IF verification_decision = 'CORRECTED'::system.verification_decision
                       AND corrected_health_id IS NULL
                       AND NOT has_corrected_item THEN
                        RAISE EXCEPTION
                            'A CORRECTED verification requires corrected health or a corrected condition item.';
                    END IF;

                    IF verification_decision <> 'CORRECTED'::system.verification_decision
                       AND (corrected_health_id IS NOT NULL OR has_corrected_item) THEN
                        RAISE EXCEPTION
                            'Only a CORRECTED verification may contain corrected values.';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE CONSTRAINT TRIGGER trg_scan_verifications_validate_correction
                AFTER INSERT ON plant.scan_verifications
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION plant.validate_corrected_scan_verification();

                CREATE CONSTRAINT TRIGGER trg_condition_reviews_validate_session
                AFTER INSERT ON plant.condition_detection_reviews
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION plant.validate_corrected_scan_verification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_condition_reviews_validate_session
                    ON plant.condition_detection_reviews;
                DROP TRIGGER IF EXISTS trg_scan_verifications_validate_correction
                    ON plant.scan_verifications;
                DROP TRIGGER IF EXISTS trg_scan_verifications_validate_revision
                    ON plant.scan_verifications;
                DROP TRIGGER IF EXISTS trg_condition_detection_reviews_immutable
                    ON plant.condition_detection_reviews;
                DROP TRIGGER IF EXISTS trg_scan_verifications_immutable
                    ON plant.scan_verifications;
                DROP TRIGGER IF EXISTS trg_condition_detections_immutable
                    ON plant.condition_detections;
                DROP FUNCTION IF EXISTS plant.validate_corrected_scan_verification();
                DROP FUNCTION IF EXISTS plant.validate_scan_verification_revision();
                DROP FUNCTION IF EXISTS plant.reject_immutable_review_mutation();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_source_task_same_farm",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_verification_of_same_plant_farm",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropForeignKey(
                name: "fk_scan_verifications_supersedes_same_scan",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropForeignKey(
                name: "fk_task_updates_created_scan_same_farm",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropForeignKey(
                name: "fk_task_updates_field_tasks_same_farm",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropTable(
                name: "condition_detection_reviews",
                schema: "plant");

            migrationBuilder.DropIndex(
                name: "IX_task_updates_created_scan_id_farm_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropIndex(
                name: "IX_task_updates_task_id_farm_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropIndex(
                name: "uq_task_updates_client_operation",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_scan_verifications_id_scan",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropIndex(
                name: "IX_scan_verifications_supersedes_verification_id_plant_scan_id",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropIndex(
                name: "uq_scan_verifications_scan_revision",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scan_verification_revision_chain",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scan_verification_revision_positive",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scan_verification_target_decision",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_plant_scans_id_plant_farm",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_source_task_id_farm_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_verification_of_scan_id_plant_id_farm_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "uq_plant_scans_client_operation",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_field_tasks_id_farm",
                schema: "field_task",
                table: "field_tasks");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_condition_detections_id_scan",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropColumn(
                name: "client_operation_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropColumn(
                name: "created_scan_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropColumn(
                name: "device_created_at",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropColumn(
                name: "farm_id",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropColumn(
                name: "server_received_at",
                schema: "field_task",
                table: "task_updates");

            migrationBuilder.DropColumn(
                name: "revision_number",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropColumn(
                name: "supersedes_verification_id",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropColumn(
                name: "client_operation_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "device_created_at",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "server_received_at",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "source_task_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "verification_of_scan_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.Sql(
                """
                UPDATE plant.scan_verifications
                SET decision = CASE
                    WHEN decision = 'CORRECTED'::system.verification_decision
                        THEN 'INCORRECT'::system.verification_decision
                    WHEN decision = 'REJECTED'::system.verification_decision
                        THEN 'INCORRECT'::system.verification_decision
                    WHEN decision = 'FIELD_INSPECTION_REQUIRED'::system.verification_decision
                        THEN 'NEED_FIELD_INSPECTION'::system.verification_decision
                    ELSE decision
                END;
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
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
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

            migrationBuilder.AlterTable(
                name: "scan_verifications",
                schema: "plant",
                comment: "Immutable verification history for manager/worker confirmation or rejection of scan results.",
                oldComment: "Immutable revisioned review sessions for manager/worker verification of scan results.");

            migrationBuilder.AddForeignKey(
                name: "fk_task_updates_field_tasks_task_id",
                schema: "field_task",
                table: "task_updates",
                column: "task_id",
                principalSchema: "field_task",
                principalTable: "field_tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
