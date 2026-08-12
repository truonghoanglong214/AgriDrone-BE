using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mission");

            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.EnsureSchema(
                name: "plant");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "farm");

            migrationBuilder.EnsureSchema(
                name: "field_task");

            migrationBuilder.EnsureSchema(
                name: "harvest");

            migrationBuilder.EnsureSchema(
                name: "notification");

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
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "ai_model_versions",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_type = table.Column<int>(type: "system.ai_model_type", nullable: false),
                    artifact_uri = table.Column<string>(type: "text", nullable: true),
                    metrics = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    trained_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_model_versions", x => x.id);
                },
                comment: "Version registry for AI models to guarantee result traceability and evaluation.");

            migrationBuilder.CreateTable(
                name: "diseases",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    scientific_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diseases", x => x.id);
                },
                comment: "Configurable disease catalog. Disease types are data, not hard-coded columns.");

            migrationBuilder.CreateTable(
                name: "drones",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "system.drone_status", nullable: false, defaultValueSql: "'AVAILABLE'::system.drone_status"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drones", x => x.id);
                },
                comment: "Physical drone inventory. A drone can be reused across multiple farms.");

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                },
                comment: "Global system roles such as SYSTEM_ADMIN.");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "citext", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<int>(type: "system.user_status", nullable: false, defaultValueSql: "'ACTIVE'::system.user_status"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                },
                comment: "System user accounts. If ASP.NET Core Identity is used, map/replace this table with AspNetUsers.");

            migrationBuilder.CreateTable(
                name: "farms",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    boundary = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    center_point = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    area_hectares = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    status = table.Column<int>(type: "system.general_status", nullable: false, defaultValueSql: "'ACTIVE'::system.general_status"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farms", x => x.id);
                    table.CheckConstraint("ck_farms_area_nonnegative", "area_hectares IS NULL OR area_hectares >= 0");
                    table.CheckConstraint("ck_farms_boundary_valid", "boundary IS NULL OR ST_IsValid(boundary)");
                    table.ForeignKey(
                        name: "fk_farms_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Top-level farm entity; stores location and optional farm polygon boundary.");

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    media_type = table.Column<int>(type: "system.media_type", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    width_px = table.Column<int>(type: "integer", nullable: true),
                    height_px = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_dimensions", "(width_px IS NULL OR width_px > 0) AND (height_px IS NULL OR height_px > 0) AND (duration_ms IS NULL OR duration_ms >= 0)");
                    table.CheckConstraint("ck_media_size", "file_size_bytes IS NULL OR file_size_bytes >= 0");
                    table.ForeignKey(
                        name: "fk_media_assets_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Metadata for files stored in Cloudinary/S3-compatible object storage; binary data is not stored in PostgreSQL.");

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "notification",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notification_read_time", "(is_read = FALSE AND read_at IS NULL) OR (is_read = TRUE AND read_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "In-app notifications such as severe disease, task assignment, or processing completion.");

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Many-to-many mapping between users and global roles.");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    old_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Append-only audit trail for sensitive business changes and traceability.");

            migrationBuilder.CreateTable(
                name: "farm_memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "system.farm_member_role", nullable: false),
                    status = table.Column<int>(type: "system.general_status", nullable: false, defaultValueSql: "'ACTIVE'::system.general_status"),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_farm_memberships_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_farm_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Farm-level authorization: one user can be OWNER, MANAGER, or WORKER in each farm.");

            migrationBuilder.CreateTable(
                name: "farm_zones",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    boundary = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    area_hectares = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    status = table.Column<int>(type: "system.general_status", nullable: false, defaultValueSql: "'ACTIVE'::system.general_status"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_zones", x => x.id);
                    table.UniqueConstraint("uq_farm_zones_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_farm_zones_area_nonnegative", "area_hectares IS NULL OR area_hectares >= 0");
                    table.CheckConstraint("ck_farm_zones_boundary_valid", "boundary IS NULL OR ST_IsValid(boundary)");
                    table.ForeignKey(
                        name: "fk_farm_zones_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_farm_zones_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Management zones inside a farm, optionally represented by polygons on the map.");

            migrationBuilder.CreateTable(
                name: "harvest_quality_grades",
                schema: "harvest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_harvest_quality_grades", x => x.id);
                    table.UniqueConstraint("uq_quality_grades_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_quality_display_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "fk_harvest_quality_grades_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Farm-configurable quality grades such as A/B/C/Rejected.");

            migrationBuilder.CreateTable(
                name: "seasons",
                schema: "harvest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "system.season_status", nullable: false, defaultValueSql: "'PLANNED'::system.season_status"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                    table.UniqueConstraint("uq_seasons_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_season_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_season_year", "year BETWEEN 2000 AND 2200");
                    table.ForeignKey(
                        name: "fk_seasons_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "A farm harvest/growing season used to aggregate productivity over time.");

            migrationBuilder.CreateTable(
                name: "drone_missions",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    drone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pilot_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mission_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mission_type = table.Column<int>(type: "system.mission_type", nullable: false),
                    status = table.Column<int>(type: "system.mission_status", nullable: false, defaultValueSql: "'DRAFT'::system.mission_status"),
                    processing_status = table.Column<int>(type: "system.processing_status", nullable: false, defaultValueSql: "'NOT_UPLOADED'::system.processing_status"),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    flight_route = table.Column<LineString>(type: "geometry(LineString,4326)", nullable: true),
                    detected_plant_count = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drone_missions", x => x.id);
                    table.UniqueConstraint("uq_drone_missions_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_drone_missions_detected_count", "detected_plant_count IS NULL OR detected_plant_count >= 0");
                    table.CheckConstraint("ck_drone_missions_time", "ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at");
                    table.ForeignKey(
                        name: "fk_drone_missions_drones_drone_id",
                        column: x => x.drone_id,
                        principalSchema: "mission",
                        principalTable: "drones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_drone_missions_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_drone_missions_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_drone_missions_users_pilot_user_id",
                        column: x => x.pilot_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_drone_missions_zone_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Drone flight mission for mapping or health inspection, including route and processing state.");

            migrationBuilder.CreateTable(
                name: "harvest_batches",
                schema: "harvest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    harvested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_fruit_count = table.Column<int>(type: "integer", nullable: true),
                    reported_weight_kg = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_harvest_batches", x => x.id);
                    table.UniqueConstraint("uq_harvest_batches_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_harvest_batch_fruit_count", "reported_fruit_count IS NULL OR reported_fruit_count >= 0");
                    table.CheckConstraint("ck_harvest_batch_weight", "reported_weight_kg IS NULL OR reported_weight_kg >= 0");
                    table.ForeignKey(
                        name: "fk_harvest_batch_season_same_farm",
                        columns: x => new { x.season_id, x.farm_id },
                        principalSchema: "harvest",
                        principalTable: "seasons",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_harvest_batch_zone_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_batches_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "One harvesting event; common data is entered once for the batch.");

            migrationBuilder.CreateTable(
                name: "ai_processing_jobs",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_type = table.Column<int>(type: "system.ai_job_type", nullable: false),
                    status = table.Column<int>(type: "system.ai_job_status", nullable: false, defaultValueSql: "'QUEUED'::system.ai_job_status"),
                    external_job_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parameters = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_processing_jobs", x => x.id);
                    table.CheckConstraint("ck_ai_job_time", "completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at");
                    table.ForeignKey(
                        name: "fk_ai_processing_jobs_drone_missions_mission_id",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "One AI processing execution for a mission. Keeps retries and failures instead of overwriting mission history.");

            migrationBuilder.CreateTable(
                name: "mission_media",
                schema: "mission",
                columns: table => new
                {
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_role = table.Column<int>(type: "system.mission_media_role", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_media", x => new { x.mission_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_mission_media_drone_missions_mission_id",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mission_media_media_assets_media_id",
                        column: x => x.media_id,
                        principalSchema: "mission",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Links raw/processed images or videos to a drone mission.");

            migrationBuilder.CreateTable(
                name: "plants",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plant_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    location = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    lifecycle_status = table.Column<int>(type: "system.plant_lifecycle_status", nullable: false, defaultValueSql: "'ACTIVE'::system.plant_lifecycle_status"),
                    current_health_status = table.Column<int>(type: "system.health_status", nullable: false, defaultValueSql: "'UNKNOWN'::system.health_status"),
                    last_inspected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mapped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_from_mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plants", x => x.id);
                    table.UniqueConstraint("uq_plants_id_farm", x => new { x.id, x.farm_id });
                    table.ForeignKey(
                        name: "fk_plants_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plants_missions_created_from_mission_id",
                        column: x => x.created_from_mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plants_zone_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Digital Plant Profile root: one row represents one real dragon-fruit pole throughout its lifecycle.");

            migrationBuilder.CreateTable(
                name: "mission_plant_observations",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tracking_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detected_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    detection_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    suggested_plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    match_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    resolved_plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_status = table.Column<int>(type: "system.observation_review_status", nullable: false, defaultValueSql: "'PENDING'::system.observation_review_status"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_plant_observations", x => x.id);
                    table.CheckConstraint("ck_observation_detection_confidence", "detection_confidence IS NULL OR detection_confidence BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_observation_match_confidence", "match_confidence IS NULL OR match_confidence BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_observation_mission_same_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_observation_resolved_plant_same_farm",
                        columns: x => new { x.resolved_plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_observation_suggested_plant_same_farm",
                        columns: x => new { x.suggested_plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_observations_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalSchema: "mission",
                        principalTable: "ai_processing_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_observations_ai_models_model_version_id",
                        column: x => x.model_version_id,
                        principalSchema: "mission",
                        principalTable: "ai_model_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_observations_media_assets_evidence_media_id",
                        column: x => x.evidence_media_id,
                        principalSchema: "mission",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_observations_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Intermediate AI detections from mapping/inspection used to match a detected object to an existing Plant ID.");

            migrationBuilder.CreateTable(
                name: "plant_change_events",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    change_type = table.Column<int>(type: "system.plant_change_type", nullable: false),
                    observed_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    status = table.Column<int>(type: "system.review_status", nullable: false, defaultValueSql: "'PENDING'::system.review_status"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_change_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_change_event_mission_same_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_change_event_plant_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plant_change_events_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Reviewable mapping differences such as missing, new, removed, or dead plants.");

            migrationBuilder.CreateTable(
                name: "plant_harvest_records",
                schema: "harvest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    harvest_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fruit_count = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_harvest_records", x => x.id);
                    table.UniqueConstraint("uq_plant_harvest_records_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_plant_harvest_fruit_count", "fruit_count >= 0");
                    table.CheckConstraint("ck_plant_harvest_weight", "weight_kg >= 0");
                    table.ForeignKey(
                        name: "fk_plant_harvest_batch_same_farm",
                        columns: x => new { x.harvest_batch_id, x.farm_id },
                        principalSchema: "harvest",
                        principalTable: "harvest_batches",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plant_harvest_plant_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Per-plant yield record within a harvest batch.");

            migrationBuilder.CreateTable(
                name: "plant_scans",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ai_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<int>(type: "system.scan_source", nullable: false),
                    overall_health_status = table.Column<int>(type: "system.health_status", nullable: false, defaultValueSql: "'UNKNOWN'::system.health_status"),
                    overall_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    review_status = table.Column<int>(type: "system.scan_review_status", nullable: false, defaultValueSql: "'PENDING'::system.scan_review_status"),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_scans", x => x.id);
                    table.UniqueConstraint("uq_plant_scans_id_farm", x => new { x.id, x.farm_id });
                    table.CheckConstraint("ck_scan_confidence", "overall_confidence IS NULL OR overall_confidence BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_plant_scans_ai_jobs_ai_job_id",
                        column: x => x.ai_job_id,
                        principalSchema: "mission",
                        principalTable: "ai_processing_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plant_scans_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_plant_scans_users_verified_by",
                        column: x => x.verified_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_scan_mission_same_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scan_plant_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Time-series health observation of a plant from drone AI, manager, or field worker.");

            migrationBuilder.CreateTable(
                name: "plant_harvest_quality_details",
                schema: "harvest",
                columns: table => new
                {
                    plant_harvest_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality_grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fruit_count = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_harvest_quality_details", x => new { x.plant_harvest_record_id, x.quality_grade_id });
                    table.CheckConstraint("ck_quality_detail_fruit_count", "fruit_count >= 0");
                    table.CheckConstraint("ck_quality_detail_weight", "weight_kg IS NULL OR weight_kg >= 0");
                    table.ForeignKey(
                        name: "fk_quality_detail_grade_same_farm",
                        columns: x => new { x.quality_grade_id, x.farm_id },
                        principalSchema: "harvest",
                        principalTable: "harvest_quality_grades",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quality_detail_record_same_farm",
                        columns: x => new { x.plant_harvest_record_id, x.farm_id },
                        principalSchema: "harvest",
                        principalTable: "plant_harvest_records",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Breakdown of one plant harvest record by configurable quality grade.");

            migrationBuilder.CreateTable(
                name: "disease_detections",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plant_scan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disease_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<int>(type: "system.finding_source", nullable: false, defaultValueSql: "'AI'::system.finding_source"),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    severity = table.Column<int>(type: "system.disease_severity", nullable: false),
                    lesion_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    affected_ratio = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    review_status = table.Column<int>(type: "system.review_status", nullable: false, defaultValueSql: "'PENDING'::system.review_status"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disease_detections", x => x.id);
                    table.CheckConstraint("ck_detection_affected_ratio", "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_detection_confidence", "confidence IS NULL OR confidence BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_detection_lesion_count", "lesion_count >= 0");
                    table.ForeignKey(
                        name: "fk_disease_detections_ai_models_model_version_id",
                        column: x => x.model_version_id,
                        principalSchema: "mission",
                        principalTable: "ai_model_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_disease_detections_diseases_disease_id",
                        column: x => x.disease_id,
                        principalSchema: "plant",
                        principalTable: "diseases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_detections_plant_scans_scan_id",
                        column: x => x.plant_scan_id,
                        principalSchema: "plant",
                        principalTable: "plant_scans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_disease_detections_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Disease findings for a plant scan, including confidence, severity, AI model and review state.");

            migrationBuilder.CreateTable(
                name: "field_tasks",
                schema: "field_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_scan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_type = table.Column<int>(type: "system.task_type", nullable: false, defaultValueSql: "'GENERAL'::system.task_type"),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "system.task_priority", nullable: false, defaultValueSql: "'MEDIUM'::system.task_priority"),
                    status = table.Column<int>(type: "system.task_status", nullable: false, defaultValueSql: "'OPEN'::system.task_status"),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_tasks_farms_farm_id",
                        column: x => x.farm_id,
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_field_tasks_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_plant_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_scan_same_farm",
                        columns: x => new { x.source_scan_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plant_scans",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Field work created by managers, often originating from an AI scan that needs human verification.");

            migrationBuilder.CreateTable(
                name: "plant_scan_media",
                schema: "plant",
                columns: table => new
                {
                    plant_scan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_role = table.Column<int>(type: "system.scan_media_role", nullable: false, defaultValueSql: "'CONTEXT'::system.scan_media_role"),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_scan_media", x => new { x.plant_scan_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_plant_scan_media_media_assets_media_id",
                        column: x => x.media_id,
                        principalSchema: "mission",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plant_scan_media_plant_scans_scan_id",
                        column: x => x.plant_scan_id,
                        principalSchema: "plant",
                        principalTable: "plant_scans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Images associated with a specific plant health scan.");

            migrationBuilder.CreateTable(
                name: "scan_verifications",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plant_scan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<int>(type: "system.verification_decision", nullable: false),
                    corrected_health_status = table.Column<int>(type: "system.health_status", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_scan_verifications_plant_scans_scan_id",
                        column: x => x.plant_scan_id,
                        principalSchema: "plant",
                        principalTable: "plant_scans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_scan_verifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Immutable verification history for manager/worker confirmation or rejection of scan results.");

            migrationBuilder.CreateTable(
                name: "disease_lesions",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    disease_detection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x_min = table.Column<decimal>(type: "numeric(8,7)", precision: 8, scale: 7, nullable: false),
                    y_min = table.Column<decimal>(type: "numeric(8,7)", precision: 8, scale: 7, nullable: false),
                    x_max = table.Column<decimal>(type: "numeric(8,7)", precision: 8, scale: 7, nullable: false),
                    y_max = table.Column<decimal>(type: "numeric(8,7)", precision: 8, scale: 7, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    affected_ratio = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disease_lesions", x => x.id);
                    table.CheckConstraint("ck_lesion_affected_ratio", "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_lesion_bbox_range", "x_min BETWEEN 0 AND 1 AND y_min BETWEEN 0 AND 1 AND x_max BETWEEN 0 AND 1 AND y_max BETWEEN 0 AND 1 AND x_min < x_max AND y_min < y_max");
                    table.CheckConstraint("ck_lesion_confidence", "confidence IS NULL OR confidence BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "fk_disease_lesions_detections_detection_id",
                        column: x => x.disease_detection_id,
                        principalSchema: "plant",
                        principalTable: "disease_detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_disease_lesions_media_assets_media_id",
                        column: x => x.media_id,
                        principalSchema: "mission",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Individual disease bounding boxes/localized affected areas on an image.");

            migrationBuilder.CreateTable(
                name: "task_assignments",
                schema: "field_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    unassigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_assignments", x => x.id);
                    table.CheckConstraint("ck_assignment_time", "unassigned_at IS NULL OR unassigned_at >= assigned_at");
                    table.ForeignKey(
                        name: "fk_task_assignments_field_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "field_task",
                        principalTable: "field_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_assignments_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_assignments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Assignment history for tasks; supports reassignment and multiple workers if needed.");

            migrationBuilder.CreateTable(
                name: "task_media",
                schema: "field_task",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_media", x => new { x.task_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_task_media_field_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "field_task",
                        principalTable: "field_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_media_media_assets_media_id",
                        column: x => x.media_id,
                        principalSchema: "mission",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_media_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Field photos or other evidence uploaded during task execution.");

            migrationBuilder.CreateTable(
                name: "task_updates",
                schema: "field_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result = table.Column<int>(type: "system.task_result", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_updates", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_updates_field_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "field_task",
                        principalTable: "field_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_updates_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Worker/manager progress and field result history for a task.");

            migrationBuilder.CreateIndex(
                name: "uq_ai_model_versions_name_version",
                schema: "mission",
                table: "ai_model_versions",
                columns: new[] { "model_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_mission",
                schema: "mission",
                table: "ai_processing_jobs",
                columns: new[] { "mission_id", "queued_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_status",
                schema: "mission",
                table: "ai_processing_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_farm",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "farm_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                schema: "system",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_disease_detections_disease",
                schema: "plant",
                table: "disease_detections",
                column: "disease_id");

            migrationBuilder.CreateIndex(
                name: "IX_disease_detections_model_version_id",
                schema: "plant",
                table: "disease_detections",
                column: "model_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_disease_detections_review",
                schema: "plant",
                table: "disease_detections",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "IX_disease_detections_reviewed_by",
                schema: "plant",
                table: "disease_detections",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_disease_detections_scan",
                schema: "plant",
                table: "disease_detections",
                column: "plant_scan_id");

            migrationBuilder.CreateIndex(
                name: "uq_detection_scan_disease",
                schema: "plant",
                table: "disease_detections",
                columns: new[] { "plant_scan_id", "disease_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disease_lesions_disease_detection_id",
                schema: "plant",
                table: "disease_lesions",
                column: "disease_detection_id");

            migrationBuilder.CreateIndex(
                name: "IX_disease_lesions_media_id",
                schema: "plant",
                table: "disease_lesions",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "uq_diseases_code",
                schema: "plant",
                table: "diseases",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_created_by",
                schema: "mission",
                table: "drone_missions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_drone_started",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "drone_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_farm_started",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "farm_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_pilot_user_id",
                schema: "mission",
                table: "drone_missions",
                column: "pilot_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_route_gist",
                schema: "mission",
                table: "drone_missions",
                column: "flight_route")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_status",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "status", "processing_status" });

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_zone_id_farm_id",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_drone_missions_farm_code",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "farm_id", "mission_code" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_farm_memberships_farm_role",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "farm_id", "role", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_memberships_user",
                schema: "identity",
                table: "farm_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_farm_memberships_farm_user",
                schema: "identity",
                table: "farm_memberships",
                columns: new[] { "farm_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_zones_boundary_gist",
                schema: "farm",
                table: "farm_zones",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_farm_zones_created_by",
                schema: "farm",
                table: "farm_zones",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_farm_zones_farm",
                schema: "farm",
                table: "farm_zones",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "ux_farm_zones_farm_code_active",
                schema: "farm",
                table: "farm_zones",
                columns: new[] { "farm_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_farms_boundary_gist",
                schema: "farm",
                table: "farms",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_farms_center_point_gist",
                schema: "farm",
                table: "farms",
                column: "center_point")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_farms_created_by",
                schema: "farm",
                table: "farms",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ux_farms_code_active",
                schema: "farm",
                table: "farms",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_field_tasks_created_by",
                schema: "field_task",
                table: "field_tasks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_field_tasks_plant_id_farm_id",
                schema: "field_task",
                table: "field_tasks",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_field_tasks_source_scan_id_farm_id",
                schema: "field_task",
                table: "field_tasks",
                columns: new[] { "source_scan_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_farm_status",
                schema: "field_task",
                table: "field_tasks",
                columns: new[] { "farm_id", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_plant",
                schema: "field_task",
                table: "field_tasks",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "IX_harvest_batches_created_by",
                schema: "harvest",
                table: "harvest_batches",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_harvest_batches_season",
                schema: "harvest",
                table: "harvest_batches",
                columns: new[] { "season_id", "harvested_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_harvest_batches_season_id_farm_id",
                schema: "harvest",
                table: "harvest_batches",
                columns: new[] { "season_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_harvest_batches_zone",
                schema: "harvest",
                table: "harvest_batches",
                columns: new[] { "zone_id", "harvested_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_harvest_batches_zone_id_farm_id",
                schema: "harvest",
                table: "harvest_batches",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_harvest_batches_farm_code",
                schema: "harvest",
                table: "harvest_batches",
                columns: new[] { "farm_id", "batch_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_quality_grades_farm_code",
                schema: "harvest",
                table: "harvest_quality_grades",
                columns: new[] { "farm_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_created",
                schema: "mission",
                table: "media_assets",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_uploaded_by",
                schema: "mission",
                table: "media_assets",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "uq_media_assets_provider_storage_key",
                schema: "mission",
                table: "media_assets",
                columns: new[] { "provider", "storage_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mission_media_media_id",
                schema: "mission",
                table: "mission_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_ai_job_id",
                schema: "mission",
                table: "mission_plant_observations",
                column: "ai_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_evidence_media_id",
                schema: "mission",
                table: "mission_plant_observations",
                column: "evidence_media_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_mission_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_model_version_id",
                schema: "mission",
                table: "mission_plant_observations",
                column: "model_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_resolved_plant_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "resolved_plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_reviewed_by",
                schema: "mission",
                table: "mission_plant_observations",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_suggested_plant_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "suggested_plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_observation_location_gist",
                schema: "mission",
                table: "mission_plant_observations",
                column: "detected_location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_observation_mission",
                schema: "mission",
                table: "mission_plant_observations",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "ix_observation_resolved_plant",
                schema: "mission",
                table: "mission_plant_observations",
                column: "resolved_plant_id");

            migrationBuilder.CreateIndex(
                name: "ux_observation_mission_tracking",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "mission_id", "tracking_id" },
                unique: true,
                filter: "tracking_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_unread",
                schema: "notification",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true },
                filter: "is_read = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_plant_change_events_mission",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "mission_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_change_events_mission_id_farm_id",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_change_events_plant",
                schema: "plant",
                table: "plant_change_events",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_change_events_plant_id_farm_id",
                schema: "plant",
                table: "plant_change_events",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_change_events_reviewed_by",
                schema: "plant",
                table: "plant_change_events",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_quality_details_plant_harvest_record_id_farm_~",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                columns: new[] { "plant_harvest_record_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_quality_details_quality_grade_id_farm_id",
                schema: "harvest",
                table: "plant_harvest_quality_details",
                columns: new[] { "quality_grade_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_harvest_records_batch",
                schema: "harvest",
                table: "plant_harvest_records",
                column: "harvest_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_records_harvest_batch_id_farm_id",
                schema: "harvest",
                table: "plant_harvest_records",
                columns: new[] { "harvest_batch_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_harvest_records_plant",
                schema: "harvest",
                table: "plant_harvest_records",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_harvest_records_plant_id_farm_id",
                schema: "harvest",
                table: "plant_harvest_records",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_plant_harvest_batch_plant",
                schema: "harvest",
                table: "plant_harvest_records",
                columns: new[] { "harvest_batch_id", "plant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plant_scan_media_media_id",
                schema: "plant",
                table: "plant_scan_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_ai_job_id",
                schema: "plant",
                table: "plant_scans",
                column: "ai_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_created_by",
                schema: "plant",
                table: "plant_scans",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_farm_health",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "farm_id", "overall_health_status", "observed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_mission",
                schema: "plant",
                table: "plant_scans",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_mission_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_plant_date",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "plant_id", "observed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_plant_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_verified_by",
                schema: "plant",
                table: "plant_scans",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "IX_plants_created_from_mission_id",
                schema: "plant",
                table: "plants",
                column: "created_from_mission_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_farm",
                schema: "plant",
                table: "plants",
                column: "farm_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_farm_lifecycle",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_id", "lifecycle_status" });

            migrationBuilder.CreateIndex(
                name: "ix_plants_location_gist",
                schema: "plant",
                table: "plants",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_plants_zone",
                schema: "plant",
                table: "plants",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_plants_zone_health",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "current_health_status" });

            migrationBuilder.CreateIndex(
                name: "IX_plants_zone_id_farm_id",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_plants_farm_code",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_id", "plant_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_roles_code",
                schema: "identity",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scan_verifications_scan",
                schema: "plant",
                table: "scan_verifications",
                columns: new[] { "plant_scan_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_scan_verifications_user_id",
                schema: "plant",
                table: "scan_verifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_farm",
                schema: "harvest",
                table: "seasons",
                columns: new[] { "farm_id", "start_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_seasons_farm_name_start",
                schema: "harvest",
                table: "seasons",
                columns: new[] { "farm_id", "name", "start_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_assignments_assigned_by",
                schema: "field_task",
                table: "task_assignments",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignments_user_active",
                schema: "field_task",
                table: "task_assignments",
                columns: new[] { "user_id", "assigned_at" },
                descending: new[] { false, true },
                filter: "unassigned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_task_active_assignment_per_user",
                schema: "field_task",
                table: "task_assignments",
                columns: new[] { "task_id", "user_id" },
                unique: true,
                filter: "unassigned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_task_media_media_id",
                schema: "field_task",
                table: "task_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_media_uploaded_by",
                schema: "field_task",
                table: "task_media",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_task_updates_task",
                schema: "field_task",
                table: "task_updates",
                columns: new[] { "task_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_task_updates_user_id",
                schema: "field_task",
                table: "task_updates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "identity",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "system");

            migrationBuilder.DropTable(
                name: "disease_lesions",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "farm_memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "mission_media",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "mission_plant_observations",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "plant_change_events",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "plant_harvest_quality_details",
                schema: "harvest");

            migrationBuilder.DropTable(
                name: "plant_scan_media",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "scan_verifications",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "task_assignments",
                schema: "field_task");

            migrationBuilder.DropTable(
                name: "task_media",
                schema: "field_task");

            migrationBuilder.DropTable(
                name: "task_updates",
                schema: "field_task");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "disease_detections",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "harvest_quality_grades",
                schema: "harvest");

            migrationBuilder.DropTable(
                name: "plant_harvest_records",
                schema: "harvest");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "field_tasks",
                schema: "field_task");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ai_model_versions",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "diseases",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "harvest_batches",
                schema: "harvest");

            migrationBuilder.DropTable(
                name: "plant_scans",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "seasons",
                schema: "harvest");

            migrationBuilder.DropTable(
                name: "ai_processing_jobs",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "plants",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "drone_missions",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "drones",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "farm_zones",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "farms",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
