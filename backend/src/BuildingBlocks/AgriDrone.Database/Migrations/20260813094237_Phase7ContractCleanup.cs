using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase7ContractCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $phase7_preflight$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM plant.plants
                        WHERE current_health_level_id IS NULL
                    ) OR EXISTS (
                        SELECT 1 FROM plant.plant_scans
                        WHERE overall_health_level_id IS NULL
                    ) OR EXISTS (
                        SELECT 1 FROM plant.condition_detections
                        WHERE severity_level_id IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 cannot contract legacy health columns while health-level backfill is incomplete.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.condition_detections AS detection
                        JOIN plant.health_levels AS level
                          ON level.id = detection.severity_level_id
                        WHERE level.rank IS NULL OR level.rank <= 0
                    ) OR EXISTS (
                        SELECT 1
                        FROM plant.condition_detection_reviews AS review
                        JOIN plant.health_levels AS level
                          ON level.id = review.corrected_severity_level_id
                        WHERE review.corrected_severity_level_id IS NOT NULL
                          AND (level.rank IS NULL OR level.rank <= 0)
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 cannot contract severity columns while a condition references a non-severity health level.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.plant_scans AS scan
                        WHERE scan.review_status <> 'PENDING'::system.scan_review_status
                          AND scan.verified_by IS NULL
                          AND NOT EXISTS (
                              SELECT 1 FROM plant.scan_verifications AS verification
                              WHERE verification.plant_scan_id = scan.id
                          )
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 cannot migrate a reviewed plant scan without a verification actor or history record.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.condition_detections AS detection
                        WHERE detection.review_status <> 'PENDING'::system.review_status
                          AND detection.reviewed_by IS NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM plant.condition_detection_reviews AS review
                              WHERE review.condition_detection_id = detection.id
                          )
                          AND NOT EXISTS (
                              SELECT 1
                              FROM plant.scan_verifications AS verification
                              WHERE verification.plant_scan_id = detection.plant_scan_id
                          )
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 cannot migrate a reviewed condition detection without an actor or review session.';
                    END IF;
                END
                $phase7_preflight$;

                DROP TRIGGER IF EXISTS trg_scan_verifications_validate_revision
                    ON plant.scan_verifications;

                CREATE TEMP TABLE phase7_review_requirements ON COMMIT DROP AS
                SELECT
                    requirement.plant_scan_id,
                    requirement.user_id,
                    MAX(requirement.reviewed_at) AS reviewed_at
                FROM (
                    SELECT
                        scan.id AS plant_scan_id,
                        scan.verified_by AS user_id,
                        COALESCE(scan.verified_at, scan.created_at) AS reviewed_at
                    FROM plant.plant_scans AS scan
                    WHERE scan.review_status <> 'PENDING'::system.scan_review_status
                      AND scan.verified_by IS NOT NULL

                    UNION ALL

                    SELECT
                        detection.plant_scan_id,
                        detection.reviewed_by,
                        COALESCE(detection.reviewed_at, detection.created_at)
                    FROM plant.condition_detections AS detection
                    WHERE detection.review_status <> 'PENDING'::system.review_status
                      AND detection.reviewed_by IS NOT NULL
                ) AS requirement
                GROUP BY requirement.plant_scan_id, requirement.user_id;

                CREATE TEMP TABLE phase7_review_session_map ON COMMIT DROP AS
                SELECT
                    requirement.plant_scan_id,
                    requirement.user_id,
                    requirement.reviewed_at,
                    COALESCE(existing.id, gen_random_uuid()) AS session_id,
                    existing.id IS NULL AS needs_insert
                FROM phase7_review_requirements AS requirement
                LEFT JOIN LATERAL (
                    SELECT verification.id
                    FROM plant.scan_verifications AS verification
                    WHERE verification.plant_scan_id = requirement.plant_scan_id
                      AND verification.user_id = requirement.user_id
                    ORDER BY verification.revision_number DESC
                    LIMIT 1
                ) AS existing ON TRUE;

                WITH existing_tail AS (
                    SELECT DISTINCT ON (verification.plant_scan_id)
                        verification.plant_scan_id,
                        verification.id AS tail_id,
                        verification.revision_number AS tail_revision
                    FROM plant.scan_verifications AS verification
                    ORDER BY verification.plant_scan_id, verification.revision_number DESC
                ),
                ranked AS (
                    SELECT
                        session_map.*,
                        COALESCE(existing_tail.tail_revision, 0) AS tail_revision,
                        existing_tail.tail_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY session_map.plant_scan_id
                            ORDER BY session_map.reviewed_at, session_map.user_id
                        ) AS new_revision_offset,
                        LAG(session_map.session_id) OVER (
                            PARTITION BY session_map.plant_scan_id
                            ORDER BY session_map.reviewed_at, session_map.user_id
                        ) AS previous_new_session_id
                    FROM phase7_review_session_map AS session_map
                    LEFT JOIN existing_tail
                      ON existing_tail.plant_scan_id = session_map.plant_scan_id
                    WHERE session_map.needs_insert
                )
                INSERT INTO plant.scan_verifications
                    (id, plant_scan_id, user_id, decision, corrected_health_level_id,
                     note, revision_number, supersedes_verification_id, created_at)
                SELECT
                    ranked.session_id,
                    ranked.plant_scan_id,
                    ranked.user_id,
                    'CONFIRMED'::system.verification_decision,
                    NULL,
                    'Migrated from legacy review projection during Phase 7 contract cleanup.',
                    ranked.tail_revision + ranked.new_revision_offset,
                    CASE
                        WHEN ranked.new_revision_offset = 1 THEN ranked.tail_id
                        ELSE ranked.previous_new_session_id
                    END,
                    ranked.reviewed_at
                FROM ranked
                ORDER BY ranked.plant_scan_id, ranked.new_revision_offset;

                INSERT INTO plant.condition_detection_reviews
                    (id, scan_verification_id, plant_scan_id, condition_detection_id,
                     decision, corrected_condition_id, corrected_severity_level_id,
                     note, created_at)
                SELECT
                    gen_random_uuid(),
                    COALESCE(session_map.session_id, fallback_verification.id),
                    detection.plant_scan_id,
                    detection.id,
                    CASE detection.review_status
                        WHEN 'CONFIRMED'::system.review_status
                            THEN 'CONFIRMED'::system.condition_review_decision
                        ELSE 'REJECTED'::system.condition_review_decision
                    END,
                    NULL,
                    NULL,
                    'Migrated from legacy condition-detection review projection during Phase 7 contract cleanup.',
                    COALESCE(detection.reviewed_at, detection.created_at)
                FROM plant.condition_detections AS detection
                LEFT JOIN phase7_review_session_map AS session_map
                  ON session_map.plant_scan_id = detection.plant_scan_id
                 AND session_map.user_id = detection.reviewed_by
                LEFT JOIN LATERAL (
                    SELECT verification.id
                    FROM plant.scan_verifications AS verification
                    WHERE verification.plant_scan_id = detection.plant_scan_id
                    ORDER BY verification.revision_number DESC
                    LIMIT 1
                ) AS fallback_verification ON TRUE
                WHERE detection.review_status <> 'PENDING'::system.review_status
                  AND NOT EXISTS (
                      SELECT 1
                      FROM plant.condition_detection_reviews AS existing_review
                      WHERE existing_review.condition_detection_id = detection.id
                  );

                DO $phase7_validation$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM plant.plant_scans AS scan
                        WHERE scan.review_status <> 'PENDING'::system.scan_review_status
                          AND NOT EXISTS (
                              SELECT 1 FROM plant.scan_verifications AS verification
                              WHERE verification.plant_scan_id = scan.id
                          )
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 review migration failed: a reviewed scan has no verification history.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.condition_detections AS detection
                        WHERE detection.review_status <> 'PENDING'::system.review_status
                          AND NOT EXISTS (
                              SELECT 1
                              FROM plant.condition_detection_reviews AS review
                              WHERE review.condition_detection_id = detection.id
                          )
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 review migration failed: a reviewed detection has no review history.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.scan_verifications AS verification
                        LEFT JOIN plant.scan_verifications AS previous
                          ON previous.id = verification.supersedes_verification_id
                         AND previous.plant_scan_id = verification.plant_scan_id
                        WHERE (verification.revision_number = 1
                               AND verification.supersedes_verification_id IS NOT NULL)
                           OR (verification.revision_number > 1
                               AND (previous.id IS NULL
                                    OR previous.revision_number <> verification.revision_number - 1))
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 7 review migration failed: verification revision history is inconsistent.';
                    END IF;
                END
                $phase7_validation$;

                CREATE TRIGGER trg_scan_verifications_validate_revision
                BEFORE INSERT ON plant.scan_verifications
                FOR EACH ROW EXECUTE FUNCTION plant.validate_scan_verification_revision();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_condition_detections_users_reviewed_by",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_users_verified_by",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "ix_plants_zone_health",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "ix_plant_scans_farm_health",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_verified_by",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "ix_condition_detections_review",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropIndex(
                name: "IX_condition_detections_reviewed_by",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropColumn(
                name: "corrected_health_status",
                schema: "plant",
                table: "scan_verifications");

            migrationBuilder.DropColumn(
                name: "current_health_status",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "overall_health_status",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "review_status",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "verified_by",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "observed_location",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "review_status",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.DropColumn(
                name: "severity",
                schema: "plant",
                table: "condition_detections");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
                .Annotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .Annotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .Annotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
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
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AlterColumn<Guid>(
                name: "current_health_level_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "overall_health_level_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "severity_level_id",
                schema: "plant",
                table: "condition_detections",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.validate_condition_severity_level()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    level_id uuid;
                BEGIN
                    IF TG_TABLE_NAME = 'condition_detections' THEN
                        level_id := NEW.severity_level_id;
                    ELSE
                        level_id := NEW.corrected_severity_level_id;
                    END IF;

                    IF level_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1
                        FROM plant.health_levels
                        WHERE id = level_id
                          AND rank > 0
                    ) THEN
                        RAISE EXCEPTION
                            'Condition severity must reference a health level with rank greater than zero.';
                    END IF;

                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_condition_detections_validate_severity
                BEFORE INSERT OR UPDATE OF severity_level_id
                    ON plant.condition_detections
                FOR EACH ROW EXECUTE FUNCTION plant.validate_condition_severity_level();

                CREATE TRIGGER trg_condition_reviews_validate_severity
                BEFORE INSERT OR UPDATE OF corrected_severity_level_id
                    ON plant.condition_detection_reviews
                FOR EACH ROW EXECUTE FUNCTION plant.validate_condition_severity_level();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_condition_reviews_validate_severity
                    ON plant.condition_detection_reviews;
                DROP TRIGGER IF EXISTS trg_condition_detections_validate_severity
                    ON plant.condition_detections;
                DROP FUNCTION IF EXISTS plant.validate_condition_severity_level();

                DROP TRIGGER IF EXISTS trg_scan_verifications_immutable
                    ON plant.scan_verifications;
                DROP TRIGGER IF EXISTS trg_condition_detections_immutable
                    ON plant.condition_detections;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
                .Annotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
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
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .OldAnnotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .OldAnnotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<int>(
                name: "corrected_health_status",
                schema: "plant",
                table: "scan_verifications",
                type: "system.health_status",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "current_health_level_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "current_health_status",
                schema: "plant",
                table: "plants",
                type: "system.health_status",
                nullable: false,
                defaultValueSql: "'UNKNOWN'::system.health_status");

            migrationBuilder.AlterColumn<Guid>(
                name: "overall_health_level_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "overall_health_status",
                schema: "plant",
                table: "plant_scans",
                type: "system.health_status",
                nullable: false,
                defaultValueSql: "'UNKNOWN'::system.health_status");

            migrationBuilder.AddColumn<int>(
                name: "review_status",
                schema: "plant",
                table: "plant_scans",
                type: "system.scan_review_status",
                nullable: false,
                defaultValueSql: "'PENDING'::system.scan_review_status");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verified_at",
                schema: "plant",
                table: "plant_scans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "observed_location",
                schema: "plant",
                table: "plant_change_events",
                type: "geometry(Point,4326)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "severity_level_id",
                schema: "plant",
                table: "condition_detections",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "review_status",
                schema: "plant",
                table: "condition_detections",
                type: "system.review_status",
                nullable: false,
                defaultValueSql: "'PENDING'::system.review_status");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "plant",
                table: "condition_detections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by",
                schema: "plant",
                table: "condition_detections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "severity",
                schema: "plant",
                table: "condition_detections",
                type: "system.disease_severity",
                nullable: false,
                defaultValueSql: "'MILD'::system.disease_severity");

            migrationBuilder.Sql(
                """
                UPDATE plant.plants AS plant
                SET current_health_status = CASE
                    WHEN level.rank IS NULL THEN 'UNKNOWN'::system.health_status
                    WHEN level.rank = 0 THEN 'HEALTHY'::system.health_status
                    WHEN level.rank = 1 THEN 'MILD'::system.health_status
                    WHEN level.rank = 2 THEN 'MODERATE'::system.health_status
                    ELSE 'SEVERE'::system.health_status
                END
                FROM plant.health_levels AS level
                WHERE level.id = plant.current_health_level_id;

                UPDATE plant.plant_scans AS scan
                SET overall_health_status = CASE
                    WHEN level.rank IS NULL THEN 'UNKNOWN'::system.health_status
                    WHEN level.rank = 0 THEN 'HEALTHY'::system.health_status
                    WHEN level.rank = 1 THEN 'MILD'::system.health_status
                    WHEN level.rank = 2 THEN 'MODERATE'::system.health_status
                    ELSE 'SEVERE'::system.health_status
                END
                FROM plant.health_levels AS level
                WHERE level.id = scan.overall_health_level_id;

                UPDATE plant.scan_verifications AS verification
                SET corrected_health_status = CASE
                    WHEN level.rank IS NULL THEN 'UNKNOWN'::system.health_status
                    WHEN level.rank = 0 THEN 'HEALTHY'::system.health_status
                    WHEN level.rank = 1 THEN 'MILD'::system.health_status
                    WHEN level.rank = 2 THEN 'MODERATE'::system.health_status
                    ELSE 'SEVERE'::system.health_status
                END
                FROM plant.health_levels AS level
                WHERE level.id = verification.corrected_health_level_id;

                UPDATE plant.condition_detections AS detection
                SET severity = CASE
                    WHEN level.rank <= 1 THEN 'MILD'::system.disease_severity
                    WHEN level.rank = 2 THEN 'MODERATE'::system.disease_severity
                    ELSE 'SEVERE'::system.disease_severity
                END
                FROM plant.health_levels AS level
                WHERE level.id = detection.severity_level_id;

                UPDATE plant.plant_change_events
                SET observed_location = new_location;

                WITH latest_verification AS (
                    SELECT DISTINCT ON (verification.plant_scan_id)
                        verification.plant_scan_id,
                        verification.user_id,
                        verification.decision,
                        verification.created_at
                    FROM plant.scan_verifications AS verification
                    ORDER BY verification.plant_scan_id, verification.revision_number DESC
                )
                UPDATE plant.plant_scans AS scan
                SET review_status = CASE latest_verification.decision
                        WHEN 'CONFIRMED'::system.verification_decision
                            THEN 'CONFIRMED'::system.scan_review_status
                        WHEN 'FIELD_INSPECTION_REQUIRED'::system.verification_decision
                            THEN 'FIELD_INSPECTION_REQUIRED'::system.scan_review_status
                        ELSE 'INCORRECT'::system.scan_review_status
                    END,
                    verified_by = latest_verification.user_id,
                    verified_at = latest_verification.created_at
                FROM latest_verification
                WHERE latest_verification.plant_scan_id = scan.id;

                WITH latest_review AS (
                    SELECT DISTINCT ON (review.condition_detection_id)
                        review.condition_detection_id,
                        review.decision,
                        verification.user_id,
                        review.created_at
                    FROM plant.condition_detection_reviews AS review
                    JOIN plant.scan_verifications AS verification
                      ON verification.id = review.scan_verification_id
                    ORDER BY review.condition_detection_id, verification.revision_number DESC
                )
                UPDATE plant.condition_detections AS detection
                SET review_status = CASE latest_review.decision
                        WHEN 'CONFIRMED'::system.condition_review_decision
                            THEN 'CONFIRMED'::system.review_status
                        ELSE 'REJECTED'::system.review_status
                    END,
                    reviewed_by = latest_review.user_id,
                    reviewed_at = latest_review.created_at
                FROM latest_review
                WHERE latest_review.condition_detection_id = detection.id;

                CREATE TRIGGER trg_condition_detections_immutable
                BEFORE UPDATE OR DELETE ON plant.condition_detections
                FOR EACH ROW EXECUTE FUNCTION plant.reject_immutable_review_mutation();

                CREATE TRIGGER trg_scan_verifications_immutable
                BEFORE UPDATE OR DELETE ON plant.scan_verifications
                FOR EACH ROW EXECUTE FUNCTION plant.reject_immutable_review_mutation();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_plants_zone_health",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "current_health_status" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_farm_health",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "farm_id", "overall_health_status", "observed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_verified_by",
                schema: "plant",
                table: "plant_scans",
                column: "verified_by");

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

            migrationBuilder.AddForeignKey(
                name: "fk_condition_detections_users_reviewed_by",
                schema: "plant",
                table: "condition_detections",
                column: "reviewed_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_users_verified_by",
                schema: "plant",
                table: "plant_scans",
                column: "verified_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
