using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase3FlightMediaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_drones_drone_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_farms_farm_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "uq_drones_code",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_serial_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.EnsureSchema(
                name: "media");

            migrationBuilder.RenameTable(
                name: "media_assets",
                schema: "mission",
                newName: "media_assets",
                newSchema: "media");

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
                .OldAnnotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
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

            migrationBuilder.AlterTable(
                name: "drones",
                schema: "mission",
                comment: "Tenant-owned physical drone inventory reusable across farms in the same tenant.",
                oldComment: "Physical drone inventory. A drone can be reused across multiple farms.");

            migrationBuilder.AddColumn<string>(
                name: "capture_clock_source",
                schema: "mission",
                table: "mission_media",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "telemetry_time_offset_ms",
                schema: "mission",
                table: "mission_media",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_maintenance_at",
                schema: "mission",
                table: "drones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                schema: "mission",
                table: "drones",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_maintenance_at",
                schema: "mission",
                table: "drones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "registration_date",
                schema: "mission",
                table: "drones",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "registration_expiry_date",
                schema: "mission",
                table: "drones",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_number",
                schema: "mission",
                table: "drones",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "specifications",
                schema: "mission",
                table: "drones",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_kg",
                schema: "mission",
                table: "drones",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "flight_parameters",
                schema: "mission",
                table: "drone_missions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "media",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "media",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_requested_at",
                schema: "media",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_id",
                schema: "media",
                table: "media_assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retention_until",
                schema: "media",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "storage_status",
                schema: "media",
                table: "media_assets",
                type: "system.media_storage_status",
                nullable: false,
                defaultValueSql: "'ACTIVE'::system.media_storage_status");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "media",
                table: "media_assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE mission.drone_missions AS mission
                SET tenant_id = farm.tenant_id
                FROM farm.farms AS farm
                WHERE farm.id = mission.farm_id;

                DO $phase3$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM mission.drone_missions
                        GROUP BY drone_id
                        HAVING COUNT(DISTINCT tenant_id) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 3 tenant backfill failed: at least one drone is used by missions from multiple tenants.';
                    END IF;
                END
                $phase3$;

                WITH drone_owners AS (
                    SELECT
                        drone_id,
                        (ARRAY_AGG(DISTINCT tenant_id))[1] AS tenant_id
                    FROM mission.drone_missions
                    GROUP BY drone_id
                )
                UPDATE mission.drones AS drone
                SET tenant_id = owner.tenant_id
                FROM drone_owners AS owner
                WHERE owner.drone_id = drone.id;

                UPDATE mission.drones
                SET tenant_id = (SELECT id FROM identity.tenants LIMIT 1)
                WHERE tenant_id IS NULL
                  AND (SELECT COUNT(*) FROM identity.tenants) = 1;

                DO $phase3$
                BEGIN
                    IF EXISTS (SELECT 1 FROM mission.drone_missions WHERE tenant_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Phase 3 tenant backfill failed: a mission references a farm without a tenant.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM mission.drones WHERE tenant_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Phase 3 tenant backfill failed: an unassigned drone has no unambiguous tenant owner.';
                    END IF;
                END
                $phase3$;

                CREATE TEMP TABLE phase3_media_owners ON COMMIT DROP AS
                SELECT mm.media_id, mission.tenant_id, mission.farm_id
                FROM mission.mission_media AS mm
                JOIN mission.drone_missions AS mission ON mission.id = mm.mission_id
                UNION ALL
                SELECT observation.evidence_media_id, mission.tenant_id, mission.farm_id
                FROM mission.mission_plant_observations AS observation
                JOIN mission.drone_missions AS mission ON mission.id = observation.mission_id
                WHERE observation.evidence_media_id IS NOT NULL
                UNION ALL
                SELECT scan_media.media_id, farm.tenant_id, scan.farm_id
                FROM plant.plant_scan_media AS scan_media
                JOIN plant.plant_scans AS scan ON scan.id = scan_media.plant_scan_id
                JOIN farm.farms AS farm ON farm.id = scan.farm_id
                UNION ALL
                SELECT lesion.media_id, farm.tenant_id, scan.farm_id
                FROM plant.disease_lesions AS lesion
                JOIN plant.disease_detections AS detection
                    ON detection.id = lesion.disease_detection_id
                JOIN plant.plant_scans AS scan ON scan.id = detection.plant_scan_id
                JOIN farm.farms AS farm ON farm.id = scan.farm_id
                UNION ALL
                SELECT task_media.media_id, farm.tenant_id, task.farm_id
                FROM field_task.task_media AS task_media
                JOIN field_task.field_tasks AS task ON task.id = task_media.task_id
                JOIN farm.farms AS farm ON farm.id = task.farm_id;

                DO $phase3$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM phase3_media_owners
                        GROUP BY media_id
                        HAVING COUNT(DISTINCT tenant_id) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Phase 3 media backfill failed: a media asset is linked to records from multiple tenants.';
                    END IF;
                END
                $phase3$;

                WITH resolved_owners AS (
                    SELECT
                        media_id,
                        (ARRAY_AGG(DISTINCT tenant_id))[1] AS tenant_id,
                        CASE
                            WHEN COUNT(DISTINCT farm_id) = 1
                            THEN (ARRAY_AGG(DISTINCT farm_id))[1]
                            ELSE NULL
                        END AS farm_id
                    FROM phase3_media_owners
                    GROUP BY media_id
                )
                UPDATE media.media_assets AS media
                SET tenant_id = owner.tenant_id,
                    farm_id = owner.farm_id
                FROM resolved_owners AS owner
                WHERE owner.media_id = media.id;

                UPDATE media.media_assets
                SET tenant_id = (SELECT id FROM identity.tenants LIMIT 1)
                WHERE tenant_id IS NULL
                  AND (SELECT COUNT(*) FROM identity.tenants) = 1;

                DO $phase3$
                BEGIN
                    IF EXISTS (SELECT 1 FROM media.media_assets WHERE tenant_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Phase 3 media backfill failed: an unlinked media asset has no unambiguous tenant owner.';
                    END IF;
                END
                $phase3$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drones",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "media",
                table: "media_assets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_drones_id_tenant",
                schema: "mission",
                table: "drones",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_drone_missions_id_tenant",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_media_assets_id_tenant",
                schema: "media",
                table: "media_assets",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.CreateTable(
                name: "mission_telemetry_points",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    altitude_m = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: true),
                    altitude_reference = table.Column<int>(type: "system.altitude_reference", nullable: true),
                    heading_deg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    speed_mps = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    horizontal_accuracy_m = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_telemetry_points", x => x.id);
                    table.CheckConstraint("ck_mission_telemetry_accuracy_nonnegative", "horizontal_accuracy_m IS NULL OR horizontal_accuracy_m >= 0");
                    table.CheckConstraint("ck_mission_telemetry_heading_range", "heading_deg IS NULL OR (heading_deg >= 0 AND heading_deg < 360)");
                    table.CheckConstraint("ck_mission_telemetry_sequence_nonnegative", "sequence_number >= 0");
                    table.CheckConstraint("ck_mission_telemetry_speed_nonnegative", "speed_mps IS NULL OR speed_mps >= 0");
                    table.ForeignKey(
                        name: "fk_mission_telemetry_points_missions_mission_id",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Timestamped drone telemetry used to interpolate frame and detection locations.");

            migrationBuilder.CreateIndex(
                name: "ix_drones_tenant",
                schema: "mission",
                table: "drones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_code",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_registration_number",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "registration_number" },
                unique: true,
                filter: "registration_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_drones_tenant_serial_number",
                schema: "mission",
                table: "drones",
                columns: new[] { "tenant_id", "serial_number" },
                unique: true,
                filter: "serial_number IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drones_maintenance_dates",
                schema: "mission",
                table: "drones",
                sql: "next_maintenance_at IS NULL OR last_maintenance_at IS NULL OR next_maintenance_at >= last_maintenance_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drones_registration_dates",
                schema: "mission",
                table: "drones",
                sql: "registration_expiry_date IS NULL OR registration_date IS NULL OR registration_expiry_date >= registration_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drones_weight_positive",
                schema: "mission",
                table: "drones",
                sql: "weight_kg IS NULL OR weight_kg > 0");

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_drone_id_tenant_id",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_farm_id_tenant_id",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_tenant",
                schema: "mission",
                table: "drone_missions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_farm_created",
                schema: "media",
                table: "media_assets",
                columns: new[] { "farm_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_farm_id_tenant_id",
                schema: "media",
                table: "media_assets",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_retention_cleanup",
                schema: "media",
                table: "media_assets",
                columns: new[] { "storage_status", "retention_until" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_tenant_created",
                schema: "media",
                table: "media_assets",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_archive_after_creation",
                schema: "media",
                table: "media_assets",
                sql: "archived_at IS NULL OR archived_at >= created_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_deletion_timeline",
                schema: "media",
                table: "media_assets",
                sql: "deletion_requested_at IS NULL OR deleted_at IS NULL OR deleted_at >= deletion_requested_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_retention_after_creation",
                schema: "media",
                table: "media_assets",
                sql: "retention_until IS NULL OR retention_until >= created_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_storage_status",
                schema: "media",
                table: "media_assets",
                sql: "(storage_status = 'DELETED'::system.media_storage_status AND deletion_requested_at IS NOT NULL AND deleted_at IS NOT NULL) OR (storage_status <> 'DELETED'::system.media_storage_status AND deleted_at IS NULL AND (storage_status NOT IN ('DELETE_PENDING'::system.media_storage_status, 'DELETE_FAILED'::system.media_storage_status) OR deletion_requested_at IS NOT NULL) AND (storage_status <> 'ARCHIVED'::system.media_storage_status OR archived_at IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "ix_mission_telemetry_location_gist",
                schema: "mission",
                table: "mission_telemetry_points",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "uq_mission_telemetry_mission_recorded_at",
                schema: "mission",
                table: "mission_telemetry_points",
                columns: new[] { "mission_id", "recorded_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mission_telemetry_mission_sequence",
                schema: "mission",
                table: "mission_telemetry_points",
                columns: new[] { "mission_id", "sequence_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_drones_same_tenant",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "tenant_id" },
                principalSchema: "mission",
                principalTable: "drones",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_farms_same_tenant",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "farm_id", "tenant_id" },
                principalSchema: "farm",
                principalTable: "farms",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_drones_tenants_tenant_id",
                schema: "mission",
                table: "drones",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_media_assets_farms_same_tenant",
                schema: "media",
                table: "media_assets",
                columns: new[] { "farm_id", "tenant_id" },
                principalSchema: "farm",
                principalTable: "farms",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_media_assets_tenants_tenant_id",
                schema: "media",
                table: "media_assets",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_drones_same_tenant",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_farms_same_tenant",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_drones_tenants_tenant_id",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropForeignKey(
                name: "fk_media_assets_farms_same_tenant",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropForeignKey(
                name: "fk_media_assets_tenants_tenant_id",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropTable(
                name: "mission_telemetry_points",
                schema: "mission");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_drones_id_tenant",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "ix_drones_tenant",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_code",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_registration_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropIndex(
                name: "uq_drones_tenant_serial_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drones_maintenance_dates",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drones_registration_dates",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drones_weight_positive",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_drone_missions_id_tenant",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "IX_drone_missions_drone_id_tenant_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "IX_drone_missions_farm_id_tenant_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "ix_drone_missions_tenant",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_media_assets_id_tenant",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_farm_created",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropIndex(
                name: "IX_media_assets_farm_id_tenant_id",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_retention_cleanup",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_tenant_created",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_archive_after_creation",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_deletion_timeline",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_retention_after_creation",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_storage_status",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "capture_clock_source",
                schema: "mission",
                table: "mission_media");

            migrationBuilder.DropColumn(
                name: "telemetry_time_offset_ms",
                schema: "mission",
                table: "mission_media");

            migrationBuilder.DropColumn(
                name: "last_maintenance_at",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "next_maintenance_at",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "registration_date",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "registration_expiry_date",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "registration_number",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "specifications",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                schema: "mission",
                table: "drones");

            migrationBuilder.DropColumn(
                name: "flight_parameters",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "deletion_requested_at",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "farm_id",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "retention_until",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "storage_status",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "media",
                table: "media_assets");

            migrationBuilder.RenameTable(
                name: "media_assets",
                schema: "media",
                newName: "media_assets",
                newSchema: "mission");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
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

            migrationBuilder.AlterTable(
                name: "drones",
                schema: "mission",
                comment: "Physical drone inventory. A drone can be reused across multiple farms.",
                oldComment: "Tenant-owned physical drone inventory reusable across farms in the same tenant.");

            migrationBuilder.CreateIndex(
                name: "uq_drones_code",
                schema: "mission",
                table: "drones",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drones_serial_number",
                schema: "mission",
                table: "drones",
                column: "serial_number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_drones_drone_id",
                schema: "mission",
                table: "drone_missions",
                column: "drone_id",
                principalSchema: "mission",
                principalTable: "drones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_farms_farm_id",
                schema: "mission",
                table: "drone_missions",
                column: "farm_id",
                principalSchema: "farm",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
