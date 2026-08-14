using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase6OfflineHarvestAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_logs_farms_farm_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_harvest_quality_grades_farms_farm_id",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropForeignKey(
                name: "fk_change_event_mission_same_farm",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropForeignKey(
                name: "fk_quality_detail_grade_same_farm",
                schema: "harvest",
                table: "plant_harvest_quality_details");

            migrationBuilder.DropIndex(
                name: "IX_plant_harvest_quality_details_quality_grade_id_farm_id",
                schema: "harvest",
                table: "plant_harvest_quality_details");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_quality_grades_id_farm",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropIndex(
                name: "uq_quality_grades_farm_code",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE phase6_quality_grade_map ON COMMIT DROP AS
                SELECT
                    id AS old_grade_id,
                    FIRST_VALUE(id) OVER (
                        PARTITION BY code
                        ORDER BY created_at, id
                    ) AS canonical_grade_id
                FROM harvest.harvest_quality_grades;

                UPDATE harvest.plant_harvest_quality_details AS detail
                SET quality_grade_id = grade_map.canonical_grade_id
                FROM phase6_quality_grade_map AS grade_map
                WHERE grade_map.old_grade_id = detail.quality_grade_id
                  AND grade_map.old_grade_id <> grade_map.canonical_grade_id;

                DELETE FROM harvest.harvest_quality_grades AS grade
                USING phase6_quality_grade_map AS grade_map
                WHERE grade_map.old_grade_id = grade.id
                  AND grade_map.old_grade_id <> grade_map.canonical_grade_id;
                """);

            migrationBuilder.DropColumn(
                name: "farm_id",
                schema: "harvest",
                table: "harvest_quality_grades");

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
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AlterTable(
                name: "plant_change_events",
                schema: "plant",
                comment: "Reviewable AI/manual plant register, retire, relocate and lifecycle changes with before/after state.",
                oldComment: "Reviewable mapping differences such as missing, new, removed, or dead plants.");

            migrationBuilder.AlterTable(
                name: "harvest_quality_grades",
                schema: "harvest",
                comment: "Global System Admin-managed quality grades such as A/B/C/Rejected.",
                oldComment: "Farm-configurable quality grades such as A/B/C/Rejected.");

            migrationBuilder.Sql(
                """
                INSERT INTO harvest.harvest_quality_grades
                    (id, code, name, display_order, is_active, created_at, updated_at)
                SELECT seed.id, seed.code, seed.name, seed.display_order, TRUE, NOW(), NOW()
                FROM (VALUES
                    ('33333333-3333-4333-8333-333333333301'::uuid, 'A', 'Grade A', 1),
                    ('33333333-3333-4333-8333-333333333302'::uuid, 'B', 'Grade B', 2),
                    ('33333333-3333-4333-8333-333333333303'::uuid, 'C', 'Grade C', 3),
                    ('33333333-3333-4333-8333-333333333304'::uuid, 'REJECTED', 'Rejected', 4)
                ) AS seed(id, code, name, display_order)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM harvest.harvest_quality_grades AS existing
                    WHERE existing.code = seed.code
                );
                """);

            migrationBuilder.AlterTable(
                name: "audit_logs",
                schema: "system",
                comment: "Append-only audit trail for user, AI and background-system actions.",
                oldComment: "Append-only audit trail for sensitive business changes and traceability.");

            migrationBuilder.AddColumn<Guid>(
                name: "client_operation_id",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "device_created_at",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recorded_at",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "server_received_at",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<int>(
                name: "source",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "system.harvest_record_source",
                nullable: false,
                defaultValueSql: "'WEB'::system.harvest_record_source");

            migrationBuilder.AlterColumn<Guid>(
                name: "mission_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "new_column_index",
                schema: "plant",
                table: "plant_change_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "new_lifecycle_status",
                schema: "plant",
                table: "plant_change_events",
                type: "system.plant_lifecycle_status",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "new_location",
                schema: "plant",
                table: "plant_change_events",
                type: "geometry(Point,4326)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "new_row_index",
                schema: "plant",
                table: "plant_change_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "old_column_index",
                schema: "plant",
                table: "plant_change_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "old_lifecycle_status",
                schema: "plant",
                table: "plant_change_events",
                type: "system.plant_lifecycle_status",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "old_location",
                schema: "plant",
                table: "plant_change_events",
                type: "geometry(Point,4326)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "old_row_index",
                schema: "plant",
                table: "plant_change_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                schema: "plant",
                table: "plant_change_events",
                type: "system.plant_change_source",
                nullable: false,
                defaultValueSql: "'MISSION_AI'::system.plant_change_source");

            migrationBuilder.AddColumn<Guid>(
                name: "farm_id",
                schema: "notification",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "notification",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "harvest",
                table: "harvest_batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "completed_by",
                schema: "harvest",
                table: "harvest_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                schema: "harvest",
                table: "harvest_batches",
                type: "system.harvest_batch_status",
                nullable: false,
                defaultValueSql: "'DRAFT'::system.harvest_batch_status");

            migrationBuilder.AddColumn<Guid>(
                name: "actor_id",
                schema: "system",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "actor_type",
                schema: "system",
                table: "audit_logs",
                type: "system.audit_actor_type",
                nullable: false,
                defaultValueSql: "'SYSTEM'::system.audit_actor_type");

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                schema: "system",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_job_id",
                schema: "system",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "system",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE harvest.plant_harvest_records AS record
                SET recorded_by = batch.created_by,
                    recorded_at = record.created_at,
                    server_received_at = record.created_at
                FROM harvest.harvest_batches AS batch
                WHERE batch.id = record.harvest_batch_id;

                UPDATE plant.plant_change_events AS change_event
                SET source = 'MISSION_AI'::system.plant_change_source,
                    new_location = COALESCE(change_event.observed_location, plant.location),
                    new_row_index = plant.row_index,
                    new_column_index = plant.column_index,
                    new_lifecycle_status = CASE change_event.change_type
                        WHEN 'NEW_PLANT'::system.plant_change_type
                            THEN 'ACTIVE'::system.plant_lifecycle_status
                        WHEN 'MISSING_PLANT'::system.plant_change_type
                            THEN 'MISSING'::system.plant_lifecycle_status
                        WHEN 'REMOVED_PLANT'::system.plant_change_type
                            THEN 'REMOVED'::system.plant_lifecycle_status
                        WHEN 'DEAD_PLANT'::system.plant_change_type
                            THEN 'DEAD'::system.plant_lifecycle_status
                        ELSE plant.lifecycle_status
                    END
                FROM plant.plants AS plant
                WHERE plant.id = change_event.plant_id
                  AND plant.farm_id = change_event.farm_id;

                UPDATE plant.plant_change_events
                SET source = 'MISSION_AI'::system.plant_change_source,
                    new_location = observed_location
                WHERE plant_id IS NULL;

                UPDATE system.audit_logs AS audit
                SET tenant_id = farm.tenant_id
                FROM farm.farms AS farm
                WHERE farm.id = audit.farm_id;

                UPDATE system.audit_logs
                SET actor_type = CASE
                        WHEN user_id IS NOT NULL THEN 'USER'::system.audit_actor_type
                        ELSE 'SYSTEM'::system.audit_actor_type
                    END,
                    actor_id = user_id;

                DO $phase6$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM harvest.plant_harvest_records
                        WHERE recorded_by IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 6 backfill failed: a harvest record has no resolvable recorder.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM plant.plant_change_events
                        WHERE NOT (
                            old_location IS DISTINCT FROM new_location OR
                            old_row_index IS DISTINCT FROM new_row_index OR
                            old_column_index IS DISTINCT FROM new_column_index OR
                            old_lifecycle_status IS DISTINCT FROM new_lifecycle_status
                        )
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 6 backfill failed: a plant change event has no resolvable before/after difference.';
                    END IF;

                    IF EXISTS (
                        SELECT code
                        FROM harvest.harvest_quality_grades
                        GROUP BY code
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 6 quality-grade remap failed: duplicate global grade codes remain.';
                    END IF;
                END
                $phase6$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "recorded_by",
                schema: "harvest",
                table: "plant_harvest_records",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_records_recorded_by",
                schema: "harvest",
                table: "plant_harvest_records",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "uq_plant_harvest_records_client_operation",
                schema: "harvest",
                table: "plant_harvest_records",
                column: "client_operation_id",
                unique: true,
                filter: "client_operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_quality_details_quality_grade_id",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                column: "quality_grade_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_change_events_created_by",
                schema: "plant",
                table: "plant_change_events",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_plant_change_events_farm_created",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "farm_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_change_has_difference",
                schema: "plant",
                table: "plant_change_events",
                sql: "old_location IS DISTINCT FROM new_location OR old_row_index IS DISTINCT FROM new_row_index OR old_column_index IS DISTINCT FROM new_column_index OR old_lifecycle_status IS DISTINCT FROM new_lifecycle_status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plant_change_source_actor",
                schema: "plant",
                table: "plant_change_events",
                sql: "(source = 'MISSION_AI'::system.plant_change_source AND mission_id IS NOT NULL) OR (source = 'MANUAL'::system.plant_change_source AND created_by IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_farm_created",
                schema: "notification",
                table: "notifications",
                columns: new[] { "farm_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_farm_id_tenant_id",
                schema: "notification",
                table: "notifications",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_created",
                schema: "notification",
                table: "notifications",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_farm_tenant_context",
                schema: "notification",
                table: "notifications",
                sql: "farm_id IS NULL OR tenant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_code",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_harvest_batches_completed_by",
                schema: "harvest",
                table: "harvest_batches",
                column: "completed_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_harvest_batch_completion",
                schema: "harvest",
                table: "harvest_batches",
                sql: "(status = 'COMPLETED'::system.harvest_batch_status AND completed_by IS NOT NULL AND completed_at IS NOT NULL) OR (status <> 'COMPLETED'::system.harvest_batch_status AND completed_by IS NULL AND completed_at IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_correlation",
                schema: "system",
                table: "audit_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_farm_id_tenant_id",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_source_job",
                schema: "system",
                table: "audit_logs",
                column: "source_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_actor_context",
                schema: "system",
                table: "audit_logs",
                sql: "(actor_type = 'USER'::system.audit_actor_type AND COALESCE(actor_id, user_id) IS NOT NULL) OR (actor_type = 'AI'::system.audit_actor_type AND source_job_id IS NOT NULL) OR actor_type = 'SYSTEM'::system.audit_actor_type");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_farm_tenant_context",
                schema: "system",
                table: "audit_logs",
                sql: "farm_id IS NULL OR tenant_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_ai_jobs_source_job_id",
                schema: "system",
                table: "audit_logs",
                column: "source_job_id",
                principalSchema: "mission",
                principalTable: "ai_processing_jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_farms_same_tenant",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "farm_id", "tenant_id" },
                principalSchema: "farm",
                principalTable: "farms",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_tenants_tenant_id",
                schema: "system",
                table: "audit_logs",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_harvest_batches_users_completed_by",
                schema: "harvest",
                table: "harvest_batches",
                column: "completed_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_farms_same_tenant",
                schema: "notification",
                table: "notifications",
                columns: new[] { "farm_id", "tenant_id" },
                principalSchema: "farm",
                principalTable: "farms",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_tenants_tenant_id",
                schema: "notification",
                table: "notifications",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_change_event_mission_same_farm",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "mission_id", "farm_id" },
                principalSchema: "mission",
                principalTable: "drone_missions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_change_events_users_created_by",
                schema: "plant",
                table: "plant_change_events",
                column: "created_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quality_detail_grade_global",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                column: "quality_grade_id",
                principalSchema: "harvest",
                principalTable: "harvest_quality_grades",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_harvest_records_users_recorded_by",
                schema: "harvest",
                table: "plant_harvest_records",
                column: "recorded_by",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION harvest.prevent_quality_grade_code_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.code IS DISTINCT FROM OLD.code THEN
                        RAISE EXCEPTION 'Harvest quality-grade code is immutable once created.';
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_harvest_quality_grades_immutable_code
                BEFORE UPDATE ON harvest.harvest_quality_grades
                FOR EACH ROW
                EXECUTE FUNCTION harvest.prevent_quality_grade_code_change();

                CREATE OR REPLACE FUNCTION harvest.reject_completed_batch_record_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF TG_OP IN ('UPDATE', 'DELETE') AND EXISTS (
                        SELECT 1
                        FROM harvest.harvest_batches
                        WHERE id = OLD.harvest_batch_id
                          AND status = 'COMPLETED'::system.harvest_batch_status
                    ) THEN
                        RAISE EXCEPTION
                            'Line items of a completed harvest batch are immutable; reopen through an audited workflow first.';
                    END IF;

                    IF TG_OP IN ('INSERT', 'UPDATE') AND EXISTS (
                        SELECT 1
                        FROM harvest.harvest_batches
                        WHERE id = NEW.harvest_batch_id
                          AND status = 'COMPLETED'::system.harvest_batch_status
                    ) THEN
                        RAISE EXCEPTION
                            'Line items cannot be added or moved into a completed harvest batch.';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_plant_harvest_records_completed_batch_guard
                BEFORE INSERT OR UPDATE OR DELETE ON harvest.plant_harvest_records
                FOR EACH ROW
                EXECUTE FUNCTION harvest.reject_completed_batch_record_mutation();

                CREATE OR REPLACE FUNCTION harvest.reject_completed_batch_quality_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    old_record_id uuid;
                    new_record_id uuid;
                BEGIN
                    IF TG_OP IN ('UPDATE', 'DELETE') THEN
                        old_record_id := OLD.plant_harvest_record_id;
                    END IF;
                    IF TG_OP IN ('INSERT', 'UPDATE') THEN
                        new_record_id := NEW.plant_harvest_record_id;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM harvest.plant_harvest_records AS record
                        JOIN harvest.harvest_batches AS batch
                          ON batch.id = record.harvest_batch_id
                        WHERE record.id IN (old_record_id, new_record_id)
                          AND batch.status = 'COMPLETED'::system.harvest_batch_status
                    ) THEN
                        RAISE EXCEPTION
                            'Quality details of a completed harvest batch are immutable; reopen through an audited workflow first.';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_harvest_quality_details_completed_batch_guard
                BEFORE INSERT OR UPDATE OR DELETE ON harvest.plant_harvest_quality_details
                FOR EACH ROW
                EXECUTE FUNCTION harvest.reject_completed_batch_quality_mutation();

                CREATE OR REPLACE FUNCTION system.reject_audit_log_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'Audit logs are append-only and cannot be updated or deleted.';
                END
                $function$;

                CREATE TRIGGER trg_audit_logs_append_only
                BEFORE UPDATE OR DELETE ON system.audit_logs
                FOR EACH ROW
                EXECUTE FUNCTION system.reject_audit_log_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_audit_logs_append_only
                    ON system.audit_logs;
                DROP TRIGGER IF EXISTS trg_harvest_quality_details_completed_batch_guard
                    ON harvest.plant_harvest_quality_details;
                DROP TRIGGER IF EXISTS trg_plant_harvest_records_completed_batch_guard
                    ON harvest.plant_harvest_records;
                DROP TRIGGER IF EXISTS trg_harvest_quality_grades_immutable_code
                    ON harvest.harvest_quality_grades;

                DROP FUNCTION IF EXISTS system.reject_audit_log_mutation();
                DROP FUNCTION IF EXISTS harvest.reject_completed_batch_quality_mutation();
                DROP FUNCTION IF EXISTS harvest.reject_completed_batch_record_mutation();
                DROP FUNCTION IF EXISTS harvest.prevent_quality_grade_code_change();

                DO $phase6_down$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM plant.plant_change_events
                        WHERE mission_id IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot roll back Phase 6 while manual plant change events without a mission exist.';
                    END IF;
                END
                $phase6_down$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_audit_logs_ai_jobs_source_job_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_logs_farms_same_tenant",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_logs_tenants_tenant_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_harvest_batches_users_completed_by",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_farms_same_tenant",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_tenants_tenant_id",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_change_event_mission_same_farm",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_change_events_users_created_by",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropForeignKey(
                name: "fk_quality_detail_grade_global",
                schema: "harvest",
                table: "plant_harvest_quality_details");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_harvest_records_users_recorded_by",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropIndex(
                name: "IX_plant_harvest_records_recorded_by",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropIndex(
                name: "uq_plant_harvest_records_client_operation",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropIndex(
                name: "IX_plant_harvest_quality_details_quality_grade_id",
                schema: "harvest",
                table: "plant_harvest_quality_details");

            migrationBuilder.DropIndex(
                name: "IX_plant_change_events_created_by",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropIndex(
                name: "ix_plant_change_events_farm_created",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_change_has_difference",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plant_change_source_actor",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropIndex(
                name: "ix_notifications_farm_created",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_farm_id_tenant_id",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_tenant_created",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_farm_tenant_context",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "uq_quality_grades_code",
                schema: "harvest",
                table: "harvest_quality_grades");

            migrationBuilder.DropIndex(
                name: "IX_harvest_batches_completed_by",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_harvest_batch_completion",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_correlation",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_farm_id_tenant_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_source_job",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_actor_context",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_farm_tenant_context",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "client_operation_id",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "device_created_at",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "recorded_at",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "recorded_by",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "server_received_at",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "harvest",
                table: "plant_harvest_records");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "new_column_index",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "new_lifecycle_status",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "new_location",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "new_row_index",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "old_column_index",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "old_lifecycle_status",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "old_location",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "old_row_index",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "plant",
                table: "plant_change_events");

            migrationBuilder.DropColumn(
                name: "farm_id",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropColumn(
                name: "completed_by",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "harvest",
                table: "harvest_batches");

            migrationBuilder.DropColumn(
                name: "actor_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "actor_type",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "source_job_id",
                schema: "system",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "system",
                table: "audit_logs");

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

            migrationBuilder.AlterTable(
                name: "plant_change_events",
                schema: "plant",
                comment: "Reviewable mapping differences such as missing, new, removed, or dead plants.",
                oldComment: "Reviewable AI/manual plant register, retire, relocate and lifecycle changes with before/after state.");

            migrationBuilder.AlterTable(
                name: "harvest_quality_grades",
                schema: "harvest",
                comment: "Farm-configurable quality grades such as A/B/C/Rejected.",
                oldComment: "Global System Admin-managed quality grades such as A/B/C/Rejected.");

            migrationBuilder.AlterTable(
                name: "audit_logs",
                schema: "system",
                comment: "Append-only audit trail for sensitive business changes and traceability.",
                oldComment: "Append-only audit trail for user, AI and background-system actions.");

            migrationBuilder.AlterColumn<Guid>(
                name: "mission_id",
                schema: "plant",
                table: "plant_change_events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_id",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE phase6_quality_grade_restore_map ON COMMIT DROP AS
                SELECT
                    grade.id AS global_grade_id,
                    farm.id AS farm_id,
                    CASE
                        WHEN ROW_NUMBER() OVER (
                            PARTITION BY grade.id
                            ORDER BY farm.id
                        ) = 1 THEN grade.id
                        ELSE gen_random_uuid()
                    END AS farm_grade_id
                FROM harvest.harvest_quality_grades AS grade
                CROSS JOIN farm.farms AS farm;

                INSERT INTO harvest.harvest_quality_grades
                    (id, code, name, display_order, is_active, created_at, updated_at, farm_id)
                SELECT
                    restore_map.farm_grade_id,
                    grade.code,
                    grade.name,
                    grade.display_order,
                    grade.is_active,
                    grade.created_at,
                    grade.updated_at,
                    restore_map.farm_id
                FROM phase6_quality_grade_restore_map AS restore_map
                JOIN harvest.harvest_quality_grades AS grade
                  ON grade.id = restore_map.global_grade_id
                WHERE restore_map.farm_grade_id <> restore_map.global_grade_id;

                UPDATE harvest.harvest_quality_grades AS grade
                SET farm_id = restore_map.farm_id
                FROM phase6_quality_grade_restore_map AS restore_map
                WHERE restore_map.global_grade_id = grade.id
                  AND restore_map.farm_grade_id = restore_map.global_grade_id;

                UPDATE harvest.plant_harvest_quality_details AS detail
                SET quality_grade_id = restore_map.farm_grade_id
                FROM phase6_quality_grade_restore_map AS restore_map
                WHERE restore_map.global_grade_id = detail.quality_grade_id
                  AND restore_map.farm_id = detail.farm_id;

                DELETE FROM harvest.harvest_quality_grades
                WHERE farm_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "farm_id",
                schema: "harvest",
                table: "harvest_quality_grades",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_quality_grades_id_farm",
                schema: "harvest",
                table: "harvest_quality_grades",
                columns: new[] { "id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_quality_details_quality_grade_id_farm_id",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                columns: new[] { "quality_grade_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_farm_code",
                schema: "harvest",
                table: "harvest_quality_grades",
                columns: new[] { "farm_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_farms_farm_id",
                schema: "system",
                table: "audit_logs",
                column: "farm_id",
                principalSchema: "farm",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_harvest_quality_grades_farms_farm_id",
                schema: "harvest",
                table: "harvest_quality_grades",
                column: "farm_id",
                principalSchema: "farm",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_change_event_mission_same_farm",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "mission_id", "farm_id" },
                principalSchema: "mission",
                principalTable: "drone_missions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_quality_detail_grade_same_farm",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                columns: new[] { "quality_grade_id", "farm_id" },
                principalSchema: "harvest",
                principalTable: "harvest_quality_grades",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
