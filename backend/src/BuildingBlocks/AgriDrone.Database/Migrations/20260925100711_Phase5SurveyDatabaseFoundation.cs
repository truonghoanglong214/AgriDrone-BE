using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase5SurveyDatabaseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "survey");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_readiness_review_status", "PENDING,REVIEWED,REJECTED")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_preflight_checklist_status", "DRAFT,COMPLETED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.mission_purpose", "BASELINE_MAPPING,PLANT_HEALTH,HARVEST_READINESS")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,SCHEDULED,IN_FLIGHT,FLIGHT_COMPLETED,UPLOADING,READY_FOR_PROCESSING,PROCESSING,AWAITING_REVIEW,COMPLETED,CANCELLED,FLIGHT_FAILED,UPLOAD_FAILED,PROCESSING_FAILED")
                .Annotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .Annotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .Annotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
                .Annotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_SCOPE_CONFIRMATION,AWAITING_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_OPERATIONS,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,CONFIRMED,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .Annotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .Annotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .Annotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .Annotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .Annotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .Annotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .Annotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,SCHEDULED,IN_FLIGHT,FLIGHT_COMPLETED,UPLOADING,READY_FOR_PROCESSING,PROCESSING,AWAITING_REVIEW,COMPLETED,CANCELLED,FLIGHT_FAILED,UPLOAD_FAILED,PROCESSING_FAILED")
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
                .OldAnnotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .OldAnnotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "farm_base_map_version_id",
                schema: "farm",
                table: "zone_map_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "survey_order_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "survey_result_id",
                schema: "plant",
                table: "plant_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "farm_base_map_version_id",
                schema: "mission",
                table: "mission_plant_observations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mission_purpose",
                schema: "mission",
                table: "drone_missions",
                type: "system.mission_purpose",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "survey_order_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "preflight_checklist_definitions",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "system.preflight_checklist_definition_status", nullable: false),
                    items = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preflight_checklist_definitions", x => x.id);
                    table.CheckConstraint("ck_preflight_definitions_items_array", "jsonb_typeof(items) = 'array'");
                    table.CheckConstraint("ck_preflight_definitions_retirement", "(status = 'RETIRED'::system.preflight_checklist_definition_status AND retired_at IS NOT NULL) OR (status <> 'RETIRED'::system.preflight_checklist_definition_status AND retired_at IS NULL)");
                    table.CheckConstraint("ck_preflight_definitions_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_preflight_definitions_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_services",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    service_type = table.Column<int>(type: "system.survey_service_type", nullable: false),
                    status = table.Column<int>(type: "system.survey_service_status", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mission_preflight_checklists",
                schema: "mission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checklist_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "system.mission_preflight_checklist_status", nullable: false),
                    definition_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    responses = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    unsuitable_condition_notes = table.Column<string>(type: "text", nullable: true),
                    failsafe_notes = table.Column<string>(type: "text", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    device_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    server_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_preflight_checklists", x => x.id);
                    table.CheckConstraint("ck_mission_preflight_completion", "(status = 'COMPLETED'::system.mission_preflight_checklist_status AND completed_by IS NOT NULL AND completed_at IS NOT NULL) OR (status <> 'COMPLETED'::system.mission_preflight_checklist_status)");
                    table.CheckConstraint("ck_mission_preflight_responses_object", "jsonb_typeof(responses) = 'object'");
                    table.CheckConstraint("ck_mission_preflight_snapshot_object", "jsonb_typeof(definition_snapshot) = 'object'");
                    table.ForeignKey(
                        name: "fk_mission_preflight_checklists_definitions_definition_id",
                        column: x => x.checklist_definition_id,
                        principalSchema: "mission",
                        principalTable: "preflight_checklist_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_preflight_checklists_missions_mission_id",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_preflight_checklists_users_completed_by",
                        column: x => x.completed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_requests",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    request_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<int>(type: "system.survey_request_kind", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    survey_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caller_scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applicant_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    applicant_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    applicant_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    farm_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    farm_address = table.Column<string>(type: "text", nullable: false),
                    approximate_area_ha = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    map_location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    estimated_pole_count = table.Column<int>(type: "integer", nullable: true),
                    preferred_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    preferred_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "system.survey_request_status", nullable: false, defaultValueSql: "'SUBMITTED'::system.survey_request_status"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_requests", x => x.id);
                    table.UniqueConstraint("uq_survey_requests_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_survey_requests_area_positive", "approximate_area_ha > 0");
                    table.CheckConstraint("ck_survey_requests_kind_context", "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL) OR (kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL) OR (kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_survey_requests_pole_count", "estimated_pole_count IS NULL OR estimated_pole_count >= 0");
                    table.CheckConstraint("ck_survey_requests_preferred_window", "preferred_end_at IS NULL OR (preferred_start_at IS NOT NULL AND preferred_end_at > preferred_start_at)");
                    table.ForeignKey(
                        name: "fk_survey_requests_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_requests_services_service_id",
                        column: x => x.survey_service_id,
                        principalSchema: "survey",
                        principalTable: "survey_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identity",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_requests_users_requested_by",
                        column: x => x.requested_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_service_prices",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_per_ha = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_service_prices", x => x.id);
                    table.UniqueConstraint("uq_survey_service_prices_id_service", x => new { x.id, x.survey_service_id });
                    table.CheckConstraint("ck_survey_service_prices_amount_positive", "price_per_ha > 0");
                    table.CheckConstraint("ck_survey_service_prices_currency", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_survey_service_prices_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_survey_service_prices_services_service_id",
                        column: x => x.survey_service_id,
                        principalSchema: "survey",
                        principalTable: "survey_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_service_prices_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_request_reviews",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<int>(type: "system.survey_review_decision", nullable: false),
                    checklist_snapshot = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_request_reviews", x => x.id);
                    table.CheckConstraint("ck_survey_request_reviews_reason", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "fk_survey_request_reviews_requests_request_id",
                        column: x => x.survey_request_id,
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_request_reviews_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_orders",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    order_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_service_price_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_survey_area_ha = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    price_per_ha_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    final_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    scope_confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requires_baseline_mapping = table.Column<bool>(type: "boolean", nullable: false),
                    previous_compatible_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "system.survey_order_status", nullable: false, defaultValueSql: "'PENDING_SCOPE_CONFIRMATION'::system.survey_order_status"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_orders", x => x.id);
                    table.UniqueConstraint("uq_survey_orders_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_survey_orders_id_farm_service", x => new { x.id, x.farm_id, x.survey_service_id });
                    table.UniqueConstraint("uq_survey_orders_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_survey_orders_area_positive", "confirmed_survey_area_ha IS NULL OR confirmed_survey_area_ha > 0");
                    table.CheckConstraint("ck_survey_orders_currency", "currency IS NULL OR currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_survey_orders_final_price", "final_price IS NULL OR final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2)");
                    table.CheckConstraint("ck_survey_orders_previous_not_self", "previous_compatible_order_id IS NULL OR previous_compatible_order_id <> id");
                    table.CheckConstraint("ck_survey_orders_price_nonnegative", "(price_per_ha_snapshot IS NULL OR price_per_ha_snapshot > 0) AND (final_price IS NULL OR final_price >= 0)");
                    table.CheckConstraint("ck_survey_orders_pricing_snapshot_complete", "(confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL AND currency IS NULL AND final_price IS NULL AND scope_confirmed_by IS NULL AND scope_confirmed_at IS NULL) OR (confirmed_survey_area_ha IS NOT NULL AND price_per_ha_snapshot IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL AND scope_confirmed_by IS NOT NULL AND scope_confirmed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_survey_orders_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_previous_same_farm_service",
                        columns: x => new { x.previous_compatible_order_id, x.farm_id, x.survey_service_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "farm_id", "survey_service_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_price_same_service",
                        columns: x => new { x.survey_service_price_id, x.survey_service_id },
                        principalSchema: "survey",
                        principalTable: "survey_service_prices",
                        principalColumns: new[] { "id", "survey_service_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_requests_request_id",
                        column: x => x.survey_request_id,
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_services_service_id",
                        column: x => x.survey_service_id,
                        principalSchema: "survey",
                        principalTable: "survey_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identity",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_orders_users_scope_confirmed_by",
                        column: x => x.scope_confirmed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "farm_base_map_versions",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    source_survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mission_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "system.farm_base_map_status", nullable: false, defaultValueSql: "'DRAFT'::system.farm_base_map_status"),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_base_map_versions", x => x.id);
                    table.UniqueConstraint("uq_farm_base_map_versions_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_farm_base_map_versions_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_farm_base_map_versions_publication", "(status = 'DRAFT'::system.farm_base_map_status AND published_by IS NULL AND published_at IS NULL) OR (status IN ('PUBLISHED'::system.farm_base_map_status, 'SUPERSEDED'::system.farm_base_map_status) AND published_by IS NOT NULL AND published_at IS NOT NULL)");
                    table.CheckConstraint("ck_farm_base_map_versions_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_farm_base_map_versions_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_base_map_versions_orders_same_tenant_farm",
                        columns: x => new { x.source_survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_base_map_versions_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_adjustments",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_area_ha = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    new_area_ha = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    old_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    new_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "system.price_adjustment_status", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_adjustments", x => x.id);
                    table.CheckConstraint("ck_price_adjustments_approval", "(status IN ('APPROVED'::system.price_adjustment_status, 'APPLIED'::system.price_adjustment_status) AND approved_by IS NOT NULL AND approved_at IS NOT NULL) OR status IN ('PENDING'::system.price_adjustment_status, 'REJECTED'::system.price_adjustment_status)");
                    table.CheckConstraint("ck_price_adjustments_area_positive", "old_area_ha > 0 AND new_area_ha > 0");
                    table.CheckConstraint("ck_price_adjustments_changed", "old_area_ha <> new_area_ha OR old_price <> new_price");
                    table.CheckConstraint("ck_price_adjustments_price_nonnegative", "old_price >= 0 AND new_price >= 0");
                    table.ForeignKey(
                        name: "fk_price_adjustments_orders_order_id",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_adjustments_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_price_adjustments_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_appointments",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proposed_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "system.survey_appointment_status", nullable: false),
                    confirmed_by_tenant_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reschedule_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_appointments", x => x.id);
                    table.CheckConstraint("ck_survey_appointments_confirmation", "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_at IS NOT NULL) OR (status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL)");
                    table.CheckConstraint("ck_survey_appointments_window", "proposed_end_at > proposed_start_at");
                    table.ForeignKey(
                        name: "fk_survey_appointments_orders_order_id",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_appointments_users_confirmed_by",
                        column: x => x.confirmed_by_tenant_owner_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_payments",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<int>(type: "system.survey_payment_status", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_payments", x => x.id);
                    table.CheckConstraint("ck_survey_payments_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_survey_payments_confirmation", "(status = 'CONFIRMED'::system.survey_payment_status AND confirmed_at IS NOT NULL AND provider_reference IS NOT NULL) OR (status <> 'CONFIRMED'::system.survey_payment_status)");
                    table.CheckConstraint("ck_survey_payments_currency", "currency ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "fk_survey_payments_orders_order_id",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_results",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type = table.Column<int>(type: "system.survey_service_type", nullable: false),
                    status = table.Column<int>(type: "system.survey_result_status", nullable: false, defaultValueSql: "'PENDING_REVIEW'::system.survey_result_status"),
                    provenance = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_results", x => x.id);
                    table.UniqueConstraint("uq_survey_results_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_survey_results_id_order_farm", x => new { x.id, x.survey_order_id, x.farm_id });
                    table.CheckConstraint("ck_survey_results_review_publication", "(status = 'PENDING_REVIEW'::system.survey_result_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND published_by IS NULL AND published_at IS NULL) OR (status = 'APPROVED'::system.survey_result_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NULL AND published_at IS NULL) OR (status = 'PUBLISHED'::system.survey_result_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_survey_results_orders_same_tenant_farm",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_results_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_results_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_events",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deduplication_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_events_payments_payment_id",
                        column: x => x.survey_payment_id,
                        principalSchema: "survey",
                        principalTable: "survey_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "harvest_readiness_assessments",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assessment_granularity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    criteria_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    visible_indicators = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    ai_assessment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ai_confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    corrected_assessment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    evidence = table.Column<JsonDocument>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    status = table.Column<int>(type: "system.harvest_readiness_review_status", nullable: false, defaultValueSql: "'PENDING'::system.harvest_readiness_review_status"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_harvest_readiness_assessments", x => x.id);
                    table.CheckConstraint("ck_harvest_readiness_confidence", "ai_confidence IS NULL OR ai_confidence BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_harvest_readiness_review", "(status = 'PENDING'::system.harvest_readiness_review_status AND reviewed_by IS NULL AND reviewed_at IS NULL) OR (status <> 'PENDING'::system.harvest_readiness_review_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_harvest_readiness_missions_same_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_readiness_plants_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_readiness_results_same_order_farm",
                        columns: x => new { x.survey_result_id, x.survey_order_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "survey_order_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_readiness_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO survey.survey_services
                    (id, code, name, description, service_type, status, created_at, updated_at)
                VALUES
                    (
                        '10000000-0000-0000-0000-000000000001',
                        'PLANT_HEALTH',
                        'Plant Health Survey',
                        'Drone imagery survey for human-verified plant health findings.',
                        'PLANT_HEALTH'::system.survey_service_type,
                        'ACTIVE'::system.survey_service_status,
                        '2026-09-25T00:00:00Z',
                        '2026-09-25T00:00:00Z'
                    ),
                    (
                        '10000000-0000-0000-0000-000000000002',
                        'HARVEST_READINESS',
                        'Harvest Readiness Survey',
                        'Experimental visible-indicator assessment of harvest readiness.',
                        'HARVEST_READINESS'::system.survey_service_type,
                        'EXPERIMENTAL'::system.survey_service_status,
                        '2026-09-25T00:00:00Z',
                        '2026-09-25T00:00:00Z'
                    );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_zone_map_versions_farm_base_map",
                schema: "farm",
                table: "zone_map_versions",
                column: "farm_base_map_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_zone_map_versions_farm_base_map_version_id_farm_id",
                schema: "farm",
                table: "zone_map_versions",
                columns: new[] { "farm_base_map_version_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_survey_order_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plant_scans_survey_result",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "survey_order_id", "survey_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plant_scans_survey_result_id_survey_order_id_farm_id",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "survey_result_id", "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_plant_observations_farm_base_map_version_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "farm_base_map_version_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_observations_farm_base_map_version",
                schema: "mission",
                table: "mission_plant_observations",
                column: "farm_base_map_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "survey_order_id", "mission_purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_drone_missions_survey_order_id_tenant_id_farm_id",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_base_map_versions_farm_id_tenant_id",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_base_map_versions_published_by",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_base_map_versions_source_survey_order_id_tenant_id_far~",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "source_survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_farm_version",
                schema: "farm",
                table: "farm_base_map_versions",
                columns: new[] { "farm_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_one_published",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "farm_id",
                unique: true,
                filter: "status = 'PUBLISHED'::system.farm_base_map_status");

            migrationBuilder.CreateIndex(
                name: "uq_farm_base_map_versions_source_order",
                schema: "farm",
                table: "farm_base_map_versions",
                column: "source_survey_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_harvest_readiness_assessments_mission_id_farm_id",
                schema: "survey",
                table: "harvest_readiness_assessments",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_harvest_readiness_assessments_plant_id_farm_id",
                schema: "survey",
                table: "harvest_readiness_assessments",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_harvest_readiness_assessments_reviewed_by",
                schema: "survey",
                table: "harvest_readiness_assessments",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_harvest_readiness_assessments_survey_result_id_survey_order~",
                schema: "survey",
                table: "harvest_readiness_assessments",
                columns: new[] { "survey_result_id", "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_harvest_readiness_profile_timeline",
                schema: "survey",
                table: "harvest_readiness_assessments",
                columns: new[] { "farm_id", "plant_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_harvest_readiness_result_plant",
                schema: "survey",
                table: "harvest_readiness_assessments",
                columns: new[] { "survey_result_id", "plant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mission_preflight_checklists_checklist_definition_id",
                schema: "mission",
                table: "mission_preflight_checklists",
                column: "checklist_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_mission_preflight_checklists_completed_by",
                schema: "mission",
                table: "mission_preflight_checklists",
                column: "completed_by");

            migrationBuilder.CreateIndex(
                name: "uq_mission_preflight_client_operation",
                schema: "mission",
                table: "mission_preflight_checklists",
                column: "client_operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mission_preflight_definition",
                schema: "mission",
                table: "mission_preflight_checklists",
                columns: new[] { "mission_id", "checklist_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mission_preflight_one_completed",
                schema: "mission",
                table: "mission_preflight_checklists",
                column: "mission_id",
                unique: true,
                filter: "status = 'COMPLETED'::system.mission_preflight_checklist_status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_events_timeline",
                schema: "survey",
                table: "payment_events",
                columns: new[] { "survey_payment_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "uq_payment_events_provider_dedup",
                schema: "survey",
                table: "payment_events",
                columns: new[] { "provider", "deduplication_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_preflight_checklist_definitions_created_by",
                schema: "mission",
                table: "preflight_checklist_definitions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "uq_preflight_definitions_code_version",
                schema: "mission",
                table: "preflight_checklist_definitions",
                columns: new[] { "code", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_preflight_definitions_one_active",
                schema: "mission",
                table: "preflight_checklist_definitions",
                column: "code",
                unique: true,
                filter: "status = 'ACTIVE'::system.preflight_checklist_definition_status");

            migrationBuilder.CreateIndex(
                name: "IX_price_adjustments_approved_by",
                schema: "survey",
                table: "price_adjustments",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_price_adjustments_order_timeline",
                schema: "survey",
                table: "price_adjustments",
                columns: new[] { "survey_order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_price_adjustments_requested_by",
                schema: "survey",
                table: "price_adjustments",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "uq_price_adjustments_one_pending_per_order",
                schema: "survey",
                table: "price_adjustments",
                column: "survey_order_id",
                unique: true,
                filter: "status = 'PENDING'::system.price_adjustment_status");

            migrationBuilder.CreateIndex(
                name: "IX_survey_appointments_confirmed_by_tenant_owner_id",
                schema: "survey",
                table: "survey_appointments",
                column: "confirmed_by_tenant_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_appointments_schedule",
                schema: "survey",
                table: "survey_appointments",
                columns: new[] { "status", "proposed_start_at" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_appointments_one_active_per_order",
                schema: "survey",
                table: "survey_appointments",
                column: "survey_order_id",
                unique: true,
                filter: "status IN ('PROPOSED'::system.survey_appointment_status, 'CONFIRMED'::system.survey_appointment_status, 'RESCHEDULE_REQUESTED'::system.survey_appointment_status)");

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_farm_id_tenant_id",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_orders_farm_service_history",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "farm_id", "survey_service_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_survey_orders_manager_work_queue",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "tenant_id", "farm_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_previous_compatible_order_id_farm_id_survey_s~",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "previous_compatible_order_id", "farm_id", "survey_service_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_scope_confirmed_by",
                schema: "survey",
                table: "survey_orders",
                column: "scope_confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_survey_service_id",
                schema: "survey",
                table: "survey_orders",
                column: "survey_service_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_survey_service_price_id_survey_service_id",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "survey_service_price_id", "survey_service_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_orders_number",
                schema: "survey",
                table: "survey_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_orders_request",
                schema: "survey",
                table: "survey_orders",
                column: "survey_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_payments_reconciliation",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_payments_one_active_per_order",
                schema: "survey",
                table: "survey_payments",
                column: "survey_order_id",
                unique: true,
                filter: "status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'CONFIRMED'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)");

            migrationBuilder.CreateIndex(
                name: "uq_survey_payments_provider_reference",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "provider", "provider_reference" },
                unique: true,
                filter: "provider_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_survey_request_reviews_reviewed_by",
                schema: "survey",
                table: "survey_request_reviews",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_survey_request_reviews_timeline",
                schema: "survey",
                table: "survey_request_reviews",
                columns: new[] { "survey_request_id", "reviewed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_requests_farm_id_tenant_id",
                schema: "survey",
                table: "survey_requests",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_requests_inbox",
                schema: "survey",
                table: "survey_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_requests_map_location_gist",
                schema: "survey",
                table: "survey_requests",
                column: "map_location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_survey_requests_requested_by_user_id",
                schema: "survey",
                table: "survey_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_requests_survey_service_id",
                schema: "survey",
                table: "survey_requests",
                column: "survey_service_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_requests_tenant_history",
                schema: "survey",
                table: "survey_requests",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_survey_requests_caller_idempotency",
                schema: "survey",
                table: "survey_requests",
                columns: new[] { "caller_scope", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_requests_number",
                schema: "survey",
                table: "survey_requests",
                column: "request_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_results_farm_status",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "tenant_id", "farm_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_results_profile_timeline",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "farm_id", "service_type", "published_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_survey_results_published_by",
                schema: "survey",
                table: "survey_results",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_survey_results_reviewed_by",
                schema: "survey",
                table: "survey_results",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_survey_results_survey_order_id_tenant_id_farm_id",
                schema: "survey",
                table: "survey_results",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_results_order",
                schema: "survey",
                table: "survey_results",
                column: "survey_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_service_prices_created_by",
                schema: "survey",
                table: "survey_service_prices",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_survey_service_prices_effective",
                schema: "survey",
                table: "survey_service_prices",
                columns: new[] { "survey_service_id", "effective_from" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_survey_services_catalogue",
                schema: "survey",
                table: "survey_services",
                columns: new[] { "status", "service_type" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_services_code",
                schema: "survey",
                table: "survey_services",
                column: "code",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_service_prices
                ADD CONSTRAINT ex_survey_service_prices_no_overlap
                EXCLUDE USING gist
                (
                    survey_service_id WITH =,
                    tstzrange(
                        effective_from,
                        COALESCE(effective_to, 'infinity'::timestamptz),
                        '[)') WITH &&
                );

                CREATE FUNCTION survey.protect_referenced_survey_service_price()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM survey.survey_orders AS orders
                        WHERE orders.survey_service_price_id = OLD.id
                    ) THEN
                        RAISE EXCEPTION
                            'Survey service price % is immutable because an order references it.',
                            OLD.id
                            USING ERRCODE = '23503';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER trg_survey_service_prices_immutable_when_referenced
                BEFORE UPDATE OR DELETE ON survey.survey_service_prices
                FOR EACH ROW
                EXECUTE FUNCTION survey.protect_referenced_survey_service_price();

                CREATE FUNCTION survey.enforce_previous_compatible_order()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NEW.previous_compatible_order_id IS NOT NULL
                       AND NOT EXISTS
                       (
                           SELECT 1
                           FROM survey.survey_orders AS previous_order
                           INNER JOIN survey.survey_results AS previous_result
                               ON previous_result.survey_order_id = previous_order.id
                           WHERE previous_order.id = NEW.previous_compatible_order_id
                             AND previous_order.farm_id = NEW.farm_id
                             AND previous_order.survey_service_id = NEW.survey_service_id
                             AND previous_order.status = 'COMPLETED'::system.survey_order_status
                             AND previous_result.status = 'PUBLISHED'::system.survey_result_status
                       ) THEN
                        RAISE EXCEPTION
                            'Previous compatible order must be completed and have a published result.'
                            USING ERRCODE = '23514';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER trg_survey_orders_previous_compatible
                AFTER INSERT OR UPDATE OF previous_compatible_order_id, farm_id, survey_service_id
                ON survey.survey_orders
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION survey.enforce_previous_compatible_order();
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_orders_same_tenant_farm",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_orders",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_observations_farm_base_map_same_farm",
                schema: "mission",
                table: "mission_plant_observations",
                columns: new[] { "farm_base_map_version_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_orders_same_farm",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "survey_order_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_orders",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_plant_scans_results_same_order_farm",
                schema: "plant",
                table: "plant_scans",
                columns: new[] { "survey_result_id", "survey_order_id", "farm_id" },
                principalSchema: "survey",
                principalTable: "survey_results",
                principalColumns: new[] { "id", "survey_order_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_zone_map_versions_farm_base_map_same_farm",
                schema: "farm",
                table: "zone_map_versions",
                columns: new[] { "farm_base_map_version_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_survey_orders_previous_compatible
                    ON survey.survey_orders;
                DROP FUNCTION IF EXISTS survey.enforce_previous_compatible_order();
                DROP TRIGGER IF EXISTS trg_survey_service_prices_immutable_when_referenced
                    ON survey.survey_service_prices;
                DROP FUNCTION IF EXISTS survey.protect_referenced_survey_service_price();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_orders_same_tenant_farm",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropForeignKey(
                name: "fk_observations_farm_base_map_same_farm",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_orders_same_farm",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropForeignKey(
                name: "fk_plant_scans_results_same_order_farm",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropForeignKey(
                name: "fk_zone_map_versions_farm_base_map_same_farm",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropTable(
                name: "farm_base_map_versions",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "harvest_readiness_assessments",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "mission_preflight_checklists",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "payment_events",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "price_adjustments",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_appointments",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_request_reviews",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_results",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "preflight_checklist_definitions",
                schema: "mission");

            migrationBuilder.DropTable(
                name: "survey_payments",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_orders",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_service_prices",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_requests",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_services",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "ix_zone_map_versions_farm_base_map",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropIndex(
                name: "IX_zone_map_versions_farm_base_map_version_id_farm_id",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_survey_order_id_farm_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "ix_plant_scans_survey_result",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_plant_scans_survey_result_id_survey_order_id_farm_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropIndex(
                name: "IX_mission_plant_observations_farm_base_map_version_id_farm_id",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropIndex(
                name: "ix_observations_farm_base_map_version",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropIndex(
                name: "ix_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "IX_drone_missions_survey_order_id_tenant_id_farm_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "farm_base_map_version_id",
                schema: "farm",
                table: "zone_map_versions");

            migrationBuilder.DropColumn(
                name: "survey_order_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "survey_result_id",
                schema: "plant",
                table: "plant_scans");

            migrationBuilder.DropColumn(
                name: "farm_base_map_version_id",
                schema: "mission",
                table: "mission_plant_observations");

            migrationBuilder.DropColumn(
                name: "mission_purpose",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "survey_order_id",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .Annotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .Annotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .Annotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .Annotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .Annotation("Npgsql:Enum:system.mission_status", "DRAFT,SCHEDULED,IN_FLIGHT,FLIGHT_COMPLETED,UPLOADING,READY_FOR_PROCESSING,PROCESSING,AWAITING_REVIEW,COMPLETED,CANCELLED,FLIGHT_FAILED,UPLOAD_FAILED,PROCESSING_FAILED")
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
                .Annotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .Annotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .Annotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .Annotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .OldAnnotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_readiness_review_status", "PENDING,REVIEWED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.map_version_status", "DRAFT,CONFIRMED,SUPERSEDED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.match_strategy", "GPS_ONLY,GRID_ASSISTED,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.media_storage_status", "ACTIVE,ARCHIVED,DELETE_PENDING,DELETED,DELETE_FAILED")
                .OldAnnotation("Npgsql:Enum:system.media_type", "IMAGE,VIDEO")
                .OldAnnotation("Npgsql:Enum:system.mission_media_role", "RAW_VIDEO,RAW_IMAGE,PROCESSED_IMAGE,THUMBNAIL,OTHER")
                .OldAnnotation("Npgsql:Enum:system.mission_preflight_checklist_status", "DRAFT,COMPLETED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.mission_purpose", "BASELINE_MAPPING,PLANT_HEALTH,HARVEST_READINESS")
                .OldAnnotation("Npgsql:Enum:system.mission_status", "DRAFT,SCHEDULED,IN_FLIGHT,FLIGHT_COMPLETED,UPLOADING,READY_FOR_PROCESSING,PROCESSING,AWAITING_REVIEW,COMPLETED,CANCELLED,FLIGHT_FAILED,UPLOAD_FAILED,PROCESSING_FAILED")
                .OldAnnotation("Npgsql:Enum:system.mission_type", "MAPPING,HEALTH_INSPECTION")
                .OldAnnotation("Npgsql:Enum:system.observation_review_status", "PENDING,MATCHED,CONFIRMED,REJECTED,NEW_PLANT,DUPLICATE")
                .OldAnnotation("Npgsql:Enum:system.plant_change_source", "MISSION_AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.plant_change_type", "NEW_PLANT,MISSING_PLANT,REMOVED_PLANT,DEAD_PLANT,DETECTION_ERROR,MAPPING_DIFFERENCE")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_SCOPE_CONFIRMATION,AWAITING_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_OPERATIONS,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,CONFIRMED,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .OldAnnotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .OldAnnotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .OldAnnotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .OldAnnotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
