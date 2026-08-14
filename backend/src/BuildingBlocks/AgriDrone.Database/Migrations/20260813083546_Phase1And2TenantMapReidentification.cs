using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase1And2TenantMapReidentification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_memberships_farms_farm_id",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropIndex(
                name: "ux_farms_code_active",
                schema: "farm",
                table: "farms");

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
                .OldAnnotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
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
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AlterTable(
                name: "farm_memberships",
                schema: "identity",
                comment: "Farm-level authorization for tenant members, optionally limited to selected zones.",
                oldComment: "Farm-level authorization: one user can be OWNER, MANAGER, or WORKER in each farm.");

            migrationBuilder.AddColumn<int>(
                name: "column_index",
                schema: "plant",
                table: "plants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_map_version_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "location_accuracy_m",
                schema: "plant",
                table: "plants",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "position_confidence",
                schema: "plant",
                table: "plants",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position_source",
                schema: "plant",
                table: "plants",
                type: "system.position_source",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "row_index",
                schema: "plant",
                table: "plants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "detected_column_index",
                schema: "mission",
                table: "mission_plant_observations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "detected_location_accuracy_m",
                schema: "mission",
                table: "mission_plant_observations",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "detected_row_index",
                schema: "mission",
                table: "mission_plant_observations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gps_distance_m",
                schema: "mission",
                table: "mission_plant_observations",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "grid_score",
                schema: "mission",
                table: "mission_plant_observations",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "map_version_id",
                schema: "mission",
                table: "mission_plant_observations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "matching_algorithm_version",
                schema: "mission",
                table: "mission_plant_observations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "matching_parameters",
                schema: "mission",
                table: "mission_plant_observations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "selected_match_strategy",
                schema: "mission",
                table: "mission_plant_observations",
                type: "system.match_strategy",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "farm",
                table: "farms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "access_scope",
                schema: "identity",
                table: "farm_memberships",
                type: "system.farm_access_scope",
                nullable: false,
                defaultValueSql: "'ALL_ZONES'::system.farm_access_scope");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "identity",
                table: "farm_memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "uq_mission_plant_observations_id_farm",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "id", "farm_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_farms_id_tenant",
                schema: "farm",
                table: "farms",
                columns: new[] { "id", "tenant_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_farm_memberships_id_farm",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "id", "farm_id" });

            migrationBuilder.CreateTable(
                name: "observation_match_candidates",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy = table.Column<int>(type: "system.match_strategy", nullable: false),
                    candidate_rank = table.Column<int>(type: "integer", nullable: false),
                    gps_distance_m = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    row_delta = table.Column<int>(type: "integer", nullable: true),
                    column_delta = table.Column<int>(type: "integer", nullable: true),
                    grid_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameters = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_observation_match_candidates", x => x.id);
                    table.CheckConstraint("ck_match_candidates_final_score", "final_score BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_match_candidates_gps_distance_nonnegative", "gps_distance_m IS NULL OR gps_distance_m >= 0");
                    table.CheckConstraint("ck_match_candidates_grid_score", "grid_score IS NULL OR grid_score BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_match_candidates_rank_positive", "candidate_rank >= 1");
                    table.ForeignKey(
                        name: "fk_match_candidates_observations_same_farm",
                        columns: x => new { x.observation_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "mission_plant_observations",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_candidates_plants_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Ranked plant candidates retained per matching strategy for reproducible re-identification evaluation.");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<int>(type: "system.general_status", nullable: false, defaultValueSql: "'ACTIVE'::system.general_status"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                },
                comment: "Top-level data isolation boundary owning farms and tenant-scoped resources.");

            migrationBuilder.CreateTable(
                name: "zone_assignments",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zone_assignments", x => x.id);
                    table.CheckConstraint("ck_zone_assignments_revoked_after_assigned", "revoked_at IS NULL OR revoked_at >= assigned_at");
                    table.ForeignKey(
                        name: "fk_zone_assignments_membership_same_farm",
                        columns: x => new { x.farm_membership_id, x.farm_id },
                        principalSchema: "identity",
                        principalTable: "farm_memberships",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_zone_assignments_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_zone_assignments_zones_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Zone access granted to a farm membership with SELECTED_ZONES scope.");

            migrationBuilder.CreateTable(
                name: "zone_map_versions",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "system.map_version_status", nullable: false, defaultValueSql: "'DRAFT'::system.map_version_status"),
                    grid_bearing_deg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    row_spacing_m = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    plant_spacing_m = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: true),
                    algorithm_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parameters = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zone_map_versions", x => x.id);
                    table.UniqueConstraint("uq_zone_map_versions_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_zone_map_versions_id_zone_farm", x => new { x.id, x.zone_id, x.farm_id });
                    table.CheckConstraint("ck_zone_map_versions_bearing_range", "grid_bearing_deg IS NULL OR (grid_bearing_deg >= 0 AND grid_bearing_deg < 360)");
                    table.CheckConstraint("ck_zone_map_versions_confirmation", "((status IN ('CONFIRMED'::system.map_version_status, 'SUPERSEDED'::system.map_version_status)) AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL) OR ((status IN ('DRAFT'::system.map_version_status, 'REJECTED'::system.map_version_status)) AND confirmed_by IS NULL AND confirmed_at IS NULL)");
                    table.CheckConstraint("ck_zone_map_versions_spacing_positive", "(row_spacing_m IS NULL OR row_spacing_m > 0) AND (plant_spacing_m IS NULL OR plant_spacing_m > 0)");
                    table.CheckConstraint("ck_zone_map_versions_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_zone_map_versions_source_mission_same_farm",
                        columns: x => new { x.source_mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_zone_map_versions_users_confirmed_by",
                        column: x => x.confirmed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_zone_map_versions_zones_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Versioned planting grid for a zone; a confirmed version is the matching baseline.");

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "system.tenant_member_role", nullable: false),
                    status = table.Column<int>(type: "system.general_status", nullable: false, defaultValueSql: "'ACTIVE'::system.general_status"),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                    table.UniqueConstraint("uq_tenant_memberships_tenant_user", x => new { x.tenant_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identity",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Tenant-level authorization and prerequisite membership for farm access.");

            migrationBuilder.Sql(
                """
                INSERT INTO identity.tenants (id, code, name, status, created_at, updated_at)
                SELECT
                    md5('agridrone:tenant:' || farm_owners.created_by::text)::uuid,
                    'MIGRATED-' || replace(farm_owners.created_by::text, '-', ''),
                    farm_owners.full_name || ' Organization',
                    'ACTIVE'::system.general_status,
                    NOW(),
                    NOW()
                FROM (
                    SELECT DISTINCT f.created_by, u.full_name
                    FROM farm.farms AS f
                    INNER JOIN identity.users AS u ON u.id = f.created_by
                ) AS farm_owners
                ON CONFLICT DO NOTHING;

                UPDATE farm.farms AS f
                SET tenant_id = md5('agridrone:tenant:' || f.created_by::text)::uuid;

                INSERT INTO identity.tenant_memberships (
                    id,
                    tenant_id,
                    user_id,
                    role,
                    status,
                    joined_at,
                    created_at)
                SELECT
                    gen_random_uuid(),
                    candidates.tenant_id,
                    candidates.user_id,
                    CASE
                        WHEN bool_or(candidates.is_owner)
                            THEN 'OWNER'::system.tenant_member_role
                        ELSE 'MEMBER'::system.tenant_member_role
                    END,
                    'ACTIVE'::system.general_status,
                    min(candidates.joined_at),
                    min(candidates.created_at)
                FROM (
                    SELECT
                        f.tenant_id,
                        f.created_by AS user_id,
                        TRUE AS is_owner,
                        f.created_at AS joined_at,
                        f.created_at
                    FROM farm.farms AS f

                    UNION ALL

                    SELECT
                        f.tenant_id,
                        fm.user_id,
                        fm.role = 'OWNER'::system.farm_member_role AS is_owner,
                        fm.joined_at,
                        fm.created_at
                    FROM identity.farm_memberships AS fm
                    INNER JOIN farm.farms AS f ON f.id = fm.farm_id
                ) AS candidates
                GROUP BY candidates.tenant_id, candidates.user_id
                ON CONFLICT (tenant_id, user_id) DO NOTHING;

                UPDATE identity.farm_memberships AS fm
                SET tenant_id = f.tenant_id
                FROM farm.farms AS f
                WHERE f.id = fm.farm_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "farm",
                table: "farms",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                schema: "identity",
                table: "farm_memberships",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_plants_current_map_version",
                schema: "plant",
                table: "plants",
                column: "current_map_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_plants_current_map_version_id_zone_id_farm_id",
                schema: "plant",
                table: "plants",
                columns: new[] { "current_map_version_id", "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ux_plants_active_zone_grid_position",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "row_index", "column_index" },
                unique: true,
                filter: "row_index IS NOT NULL AND column_index IS NOT NULL AND lifecycle_status IN ('ACTIVE'::system.plant_lifecycle_status, 'MISSING'::system.plant_lifecycle_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_grid_indices_positive",
                schema: "plant",
                table: "plants",
                sql: "(row_index IS NULL OR row_index >= 1) AND (column_index IS NULL OR column_index >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants",
                sql: "(current_map_version_id IS NULL AND row_index IS NULL AND column_index IS NULL) OR (current_map_version_id IS NOT NULL AND zone_id IS NOT NULL AND row_index IS NOT NULL AND column_index IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_location_accuracy_nonnegative",
                schema: "plant",
                table: "plants",
                sql: "location_accuracy_m IS NULL OR location_accuracy_m >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_position_confidence",
                schema: "plant",
                table: "plants",
                sql: "position_confidence IS NULL OR position_confidence BETWEEN 0 AND 1");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_map_version_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "map_version_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_map_grid_position",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "map_version_id", "detected_row_index", "detected_column_index" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_map_version",
                schema: "mission",
                table: "mission_plant_observations",
                column: "map_version_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_observation_gps_distance_nonnegative",
                schema: "mission",
                table: "mission_plant_observations",
                sql: "gps_distance_m IS NULL OR gps_distance_m >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_observation_grid_indices_positive",
                schema: "mission",
                table: "mission_plant_observations",
                sql: "(detected_row_index IS NULL OR detected_row_index >= 1) AND (detected_column_index IS NULL OR detected_column_index >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_observation_grid_position_complete",
                schema: "mission",
                table: "mission_plant_observations",
                sql: "(detected_row_index IS NULL AND detected_column_index IS NULL) OR (map_version_id IS NOT NULL AND detected_row_index IS NOT NULL AND detected_column_index IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_observation_grid_score",
                schema: "mission",
                table: "mission_plant_observations",
                sql: "grid_score IS NULL OR grid_score BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_observation_location_accuracy_nonnegative",
                schema: "mission",
                table: "mission_plant_observations",
                sql: "detected_location_accuracy_m IS NULL OR detected_location_accuracy_m >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_farms_tenant",
                schema: "farm",
                table: "farms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_farms_tenant_code_active",
                schema: "farm",
                table: "farms",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_farm_memberships_farm_id_tenant_id",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_memberships_tenant_user",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_match_candidates_plant",
                schema: "mission",
                table: "observation_match_candidates",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "IX_observation_match_candidates_observation_id_farm_id",
                schema: "mission",
                table: "observation_match_candidates",
                columns: new[] { "observation_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_match_candidates_plant_id_farm_id",
                schema: "mission",
                table: "observation_match_candidates",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_match_candidates_observation_strategy_plant",
                schema: "mission",
                table: "observation_match_candidates",
                columns: new[] { "observation_id", "strategy", "plant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_match_candidates_observation_strategy_rank",
                schema: "mission",
                table: "observation_match_candidates",
                columns: new[] { "observation_id", "strategy", "candidate_rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_tenant_role",
                schema: "identity",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "role", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user",
                schema: "identity",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_tenants_code_active",
                schema: "identity",
                table: "tenants",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_zone_assignments_assigned_by",
                schema: "identity",
                table: "zone_assignments",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_zone_assignments_farm_membership_id_farm_id",
                schema: "identity",
                table: "zone_assignments",
                columns: new[] { "farm_membership_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_zone_assignments_farm_zone",
                schema: "identity",
                table: "zone_assignments",
                columns: new[] { "farm_id", "zone_id" });

            migrationBuilder.CreateIndex(
                name: "IX_zone_assignments_zone_id_farm_id",
                schema: "identity",
                table: "zone_assignments",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ux_zone_assignments_membership_zone_active",
                schema: "identity",
                table: "zone_assignments",
                columns: new[] { "farm_membership_id", "zone_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_zone_map_versions_confirmed_by",
                schema: "farm",
                table: "zone_map_versions",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "ix_zone_map_versions_source_mission",
                schema: "farm",
                table: "zone_map_versions",
                column: "source_mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_zone_map_versions_source_mission_id_farm_id",
                schema: "farm",
                table: "zone_map_versions",
                columns: new[] { "source_mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_zone_map_versions_zone_id_farm_id",
                schema: "farm",
                table: "zone_map_versions",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_zone_map_versions_zone_version",
                schema: "farm",
                table: "zone_map_versions",
                columns: new[] { "zone_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_zone_map_versions_one_confirmed",
                schema: "farm",
                table: "zone_map_versions",
                column: "zone_id",
                unique: true,
                filter: "status = 'CONFIRMED'::system.map_version_status");

            migrationBuilder.AddForeignKey(
                name: "fk_farm_memberships_farms_same_tenant",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "farm_id", "tenant_id" },
                principalSchema: "farm",
                principalTable: "farms",
                principalColumns: new[] { "id", "tenant_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_farm_memberships_tenant_members_same_tenant",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "tenant_id", "user_id" },
                principalSchema: "identity",
                principalTable: "tenant_memberships",
                principalColumns: new[] { "tenant_id", "user_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_farms_tenants_tenant_id",
                schema: "farm",
                table: "farms",
                column: "tenant_id",
                principalSchema: "identity",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_observations_map_versions_same_farm",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "map_version_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "zone_map_versions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plants_current_map_version_same_zone",
                schema: "plant",
                table: "plants",
                columns: new[] { "current_map_version_id", "zone_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "zone_map_versions",
                principalColumns: new[] { "id", "zone_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_farm_memberships_farms_same_tenant",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_farm_memberships_tenant_members_same_tenant",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_farms_tenants_tenant_id",
                schema: "farm",
                table: "farms");

            migrationBuilder.DropForeignKey(
                name: "fk_observations_map_versions_same_farm",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropForeignKey(
                name: "fk_plants_current_map_version_same_zone",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropTable(
                name: "observation_match_candidates",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "zone_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "zone_map_versions",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_plants_current_map_version",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "IX_plants_current_map_version_id_zone_id_farm_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "ux_plants_active_zone_grid_position",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_grid_indices_positive",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_location_accuracy_nonnegative",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_position_confidence",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_mission_plant_observations_id_farm",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropIndex(
                name: "IX_mission_plant_observations_map_version_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropIndex(
                name: "ix_observations_map_grid_position",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropIndex(
                name: "ix_observations_map_version",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_observation_gps_distance_nonnegative",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_observation_grid_indices_positive",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_observation_grid_position_complete",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_observation_grid_score",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_observation_location_accuracy_nonnegative",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_farms_id_tenant",
                schema: "farm",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ix_farms_tenant",
                schema: "farm",
                table: "farms");

            migrationBuilder.DropIndex(
                name: "ux_farms_tenant_code_active",
                schema: "farm",
                table: "farms");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_farm_memberships_id_farm",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropIndex(
                name: "IX_farm_memberships_farm_id_tenant_id",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropIndex(
                name: "ix_farm_memberships_tenant_user",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropColumn(
                name: "column_index",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "current_map_version_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "location_accuracy_m",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "position_confidence",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "position_source",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "row_index",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "detected_column_index",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "detected_location_accuracy_m",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "detected_row_index",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "gps_distance_m",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "grid_score",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "map_version_id",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "matching_algorithm_version",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "matching_parameters",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "selected_match_strategy",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "farm",
                table: "farms");

            migrationBuilder.DropColumn(
                name: "access_scope",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "identity",
                table: "farm_memberships");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.disease_severity", "MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE")
                .Annotation("Npgsql:Enum:system.farm_member_role", "OWNER,MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.health_status", "UNKNOWN,HEALTHY,MILD,MODERATE,SEVERE")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,READY,FLYING,COMPLETED,CANCELLED,FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
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
                name: "farm_memberships",
                schema: "identity",
                comment: "Farm-level authorization: one user can be OWNER, MANAGER, or WORKER in each farm.",
                oldComment: "Farm-level authorization for tenant members, optionally limited to selected zones.");

            migrationBuilder.CreateIndex(
                name: "ux_farms_code_active",
                schema: "farm",
                table: "farms",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_farm_memberships_farms_farm_id",
                schema: "identity",
                table: "farm_memberships",
                column: "farm_id",
                principalSchema: "farm",
                principalTable: "farms",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
