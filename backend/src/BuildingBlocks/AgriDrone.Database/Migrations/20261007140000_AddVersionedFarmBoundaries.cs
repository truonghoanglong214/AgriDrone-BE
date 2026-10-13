using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedFarmBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.boundary_exception_decision", "ACCEPTED_INSIDE,REJECTED_OUTSIDE,LOCATION_CORRECTED")
                .Annotation("Npgsql:Enum:system.boundary_exception_source", "BASELINE_CANDIDATE,HEALTH_OBSERVATION,PLANT_INVENTORY_CHANGE,MANUAL")
                .Annotation("Npgsql:Enum:system.boundary_exception_state", "OUT_OF_BOUNDARY,NEEDS_REVIEW,RESOLVED")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,LEGACY_IMPORT")
                .Annotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
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
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM survey.survey_orders
                        WHERE farm_boundary_version_id IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = 'P0001',
                            MESSAGE = '0R-3 upgrade blocked: existing survey order boundary references cannot be mapped safely; reconcile them before migration.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM farm.farms
                        WHERE boundary IS NOT NULL
                          AND (ST_IsEmpty(boundary) OR NOT ST_IsValid(boundary))
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = 'P0001',
                            MESSAGE = '0R-3 upgrade blocked: invalid legacy farm boundaries require remediation before migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "farm_boundaries",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    geometry = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: false),
                    source = table.Column<int>(type: "system.farm_boundary_source", nullable: false),
                    source_survey_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "system.farm_boundary_status", nullable: false, defaultValueSql: "'DRAFT'::system.farm_boundary_status"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    superseded_by_boundary_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_boundaries", x => x.id);
                    table.UniqueConstraint("uq_farm_boundaries_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_farm_boundaries_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_farm_boundaries_geometry_valid", "NOT ST_IsEmpty(geometry) AND ST_IsValid(geometry) AND ST_SRID(geometry) = 4326 AND GeometryType(geometry) = 'POLYGON'");
                    table.CheckConstraint("ck_farm_boundaries_legacy_source", "source <> 'LEGACY_IMPORT'::system.farm_boundary_source OR source_survey_request_id IS NULL");
                    table.CheckConstraint("ck_farm_boundaries_review_state", "(status = 'DRAFT'::system.farm_boundary_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR (status = 'APPROVED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NOT NULL AND superseded_at IS NOT NULL)");
                    table.CheckConstraint("ck_farm_boundaries_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_farm_boundaries_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_boundaries_replacement_same_farm",
                        columns: x => new { x.superseded_by_boundary_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_boundaries_requests_same_tenant_farm",
                        columns: x => new { x.source_survey_request_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_requests",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_boundaries_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_boundaries_users_submitted_by",
                        column: x => x.submitted_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boundary_exceptions",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<int>(type: "system.boundary_exception_source", nullable: false),
                    source_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_position = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    state = table.Column<int>(type: "system.boundary_exception_state", nullable: false),
                    measured_distance_meters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    threshold_meters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decision = table.Column<int>(type: "system.boundary_exception_decision", nullable: true),
                    corrected_position = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    review_evidence = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boundary_exceptions", x => x.id);
                    table.CheckConstraint("ck_boundary_exceptions_corrected_position", "corrected_position IS NULL OR (NOT ST_IsEmpty(corrected_position) AND ST_IsValid(corrected_position) AND ST_SRID(corrected_position) = 4326 AND GeometryType(corrected_position) = 'POINT')");
                    table.CheckConstraint("ck_boundary_exceptions_distance_nonnegative", "measured_distance_meters >= 0");
                    table.CheckConstraint("ck_boundary_exceptions_original_position", "NOT ST_IsEmpty(original_position) AND ST_IsValid(original_position) AND ST_SRID(original_position) = 4326 AND GeometryType(original_position) = 'POINT'");
                    table.CheckConstraint("ck_boundary_exceptions_resolution", "(state IN ('OUT_OF_BOUNDARY'::system.boundary_exception_state, 'NEEDS_REVIEW'::system.boundary_exception_state) AND decision IS NULL AND corrected_position IS NULL AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL) OR (state = 'RESOLVED'::system.boundary_exception_state AND decision IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND ((decision = 'LOCATION_CORRECTED'::system.boundary_exception_decision AND corrected_position IS NOT NULL) OR (decision <> 'LOCATION_CORRECTED'::system.boundary_exception_decision AND corrected_position IS NULL)))");
                    table.CheckConstraint("ck_boundary_exceptions_threshold_positive", "threshold_meters > 0");
                    table.ForeignKey(
                        name: "fk_boundary_exceptions_boundary_same_tenant_farm",
                        columns: x => new { x.farm_boundary_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boundary_exceptions_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boundary_exceptions_missions_same_farm",
                        columns: x => new { x.mission_id, x.farm_id },
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boundary_exceptions_orders_same_tenant_farm",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boundary_exceptions_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_orders_farm_boundary_version_id_tenant_id_farm_id",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_boundary_exceptions_corrected_position_gist",
                schema: "farm",
                table: "boundary_exceptions",
                column: "corrected_position")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_boundary_exceptions_farm_boundary_version_id_tenant_id_farm~",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_boundary_exceptions_farm_id_tenant_id",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_boundary_exceptions_mission_id_farm_id",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "mission_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_boundary_exceptions_original_position_gist",
                schema: "farm",
                table: "boundary_exceptions",
                column: "original_position")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_boundary_exceptions_review_queue",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "farm_id", "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_boundary_exceptions_reviewed_by",
                schema: "farm",
                table: "boundary_exceptions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_boundary_exceptions_survey_order_id_tenant_id_farm_id",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_boundary_exceptions_unresolved_source",
                schema: "farm",
                table: "boundary_exceptions",
                columns: new[] { "farm_boundary_version_id", "source", "source_reference_id" },
                unique: true,
                filter: "state <> 'RESOLVED'::system.boundary_exception_state");

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_farm_id_tenant_id",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_boundaries_geometry_gist",
                schema: "farm",
                table: "farm_boundaries",
                column: "geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_reviewed_by",
                schema: "farm",
                table: "farm_boundaries",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_source_survey_request_id_tenant_id_farm_id",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "source_survey_request_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_submitted_by",
                schema: "farm",
                table: "farm_boundaries",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_boundaries_superseded_by_boundary_id_farm_id",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "superseded_by_boundary_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_boundaries_farm_version",
                schema: "farm",
                table: "farm_boundaries",
                columns: new[] { "farm_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_farm_boundaries_one_approved",
                schema: "farm",
                table: "farm_boundaries",
                column: "farm_id",
                unique: true,
                filter: "status = 'APPROVED'::system.farm_boundary_status");

            migrationBuilder.Sql(
                """
                INSERT INTO farm.farm_boundaries (
                    id,
                    tenant_id,
                    farm_id,
                    version_number,
                    geometry,
                    source,
                    submitted_by,
                    status,
                    created_at,
                    updated_at
                )
                SELECT
                    gen_random_uuid(),
                    tenant_id,
                    id,
                    1,
                    boundary,
                    'LEGACY_IMPORT'::system.farm_boundary_source,
                    created_by,
                    'DRAFT'::system.farm_boundary_status,
                    created_at,
                    updated_at
                FROM farm.farms
                WHERE boundary IS NOT NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_orders_farm_boundary_same_tenant_farm",
                schema: "survey",
                table: "survey_orders",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_boundaries",
                principalColumns: new[] { "id", "tenant_id", "farm_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION farm.enforce_farm_boundary_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT'::system.farm_boundary_status THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Reviewed FarmBoundary history cannot be deleted.';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF NEW.id <> OLD.id
                       OR NEW.tenant_id <> OLD.tenant_id
                       OR NEW.farm_id <> OLD.farm_id
                       OR NEW.version_number <> OLD.version_number
                       OR NEW.source <> OLD.source
                       OR NEW.source_survey_request_id IS DISTINCT FROM OLD.source_survey_request_id
                       OR NEW.submitted_by IS DISTINCT FROM OLD.submitted_by THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'FarmBoundary identity and provenance are immutable.';
                    END IF;

                    IF OLD.status <> 'DRAFT'::system.farm_boundary_status
                       AND NOT ST_Equals(NEW.geometry, OLD.geometry) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Reviewed FarmBoundary geometry is immutable.';
                    END IF;

                    IF (OLD.status = 'DRAFT'::system.farm_boundary_status
                        AND NEW.status NOT IN ('DRAFT'::system.farm_boundary_status, 'APPROVED'::system.farm_boundary_status, 'REJECTED'::system.farm_boundary_status))
                       OR (OLD.status = 'APPROVED'::system.farm_boundary_status
                           AND NEW.status NOT IN ('APPROVED'::system.farm_boundary_status, 'SUPERSEDED'::system.farm_boundary_status))
                       OR (OLD.status IN ('REJECTED'::system.farm_boundary_status, 'SUPERSEDED'::system.farm_boundary_status)
                           AND NEW.status <> OLD.status) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Invalid FarmBoundary status transition.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_farm_boundaries_protect_history
                BEFORE UPDATE OR DELETE ON farm.farm_boundaries
                FOR EACH ROW EXECUTE FUNCTION farm.enforce_farm_boundary_history();

                CREATE OR REPLACE FUNCTION farm.validate_approved_boundary_zones()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF NEW.status <> 'APPROVED'::system.farm_boundary_status THEN
                        RETURN NEW;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM farm.farm_zones z
                        WHERE z.farm_id = NEW.farm_id
                          AND z.deleted_at IS NULL
                          AND z.status = 'ACTIVE'::system.general_status
                          AND (z.boundary IS NULL OR NOT ST_Covers(NEW.geometry, z.boundary))
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Every active Zone must have a boundary covered by the approved FarmBoundary.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM farm.farm_zones left_zone
                        JOIN farm.farm_zones right_zone
                          ON right_zone.farm_id = left_zone.farm_id
                         AND right_zone.id > left_zone.id
                        WHERE left_zone.farm_id = NEW.farm_id
                          AND left_zone.deleted_at IS NULL
                          AND right_zone.deleted_at IS NULL
                          AND left_zone.status = 'ACTIVE'::system.general_status
                          AND right_zone.status = 'ACTIVE'::system.general_status
                          AND ST_Intersects(left_zone.boundary, right_zone.boundary)
                          AND NOT ST_Touches(left_zone.boundary, right_zone.boundary)
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Active Zone boundaries cannot overlap.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_farm_boundaries_validate_zones
                BEFORE INSERT OR UPDATE OF status, geometry ON farm.farm_boundaries
                FOR EACH ROW EXECUTE FUNCTION farm.validate_approved_boundary_zones();

                CREATE OR REPLACE FUNCTION farm.enforce_zone_against_approved_boundary()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    approved_geometry geometry(Polygon, 4326);
                BEGIN
                    IF NEW.deleted_at IS NOT NULL OR NEW.status <> 'ACTIVE'::system.general_status THEN
                        RETURN NEW;
                    END IF;

                    SELECT geometry INTO approved_geometry
                    FROM farm.farm_boundaries
                    WHERE farm_id = NEW.farm_id
                      AND status = 'APPROVED'::system.farm_boundary_status;

                    IF approved_geometry IS NULL THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.boundary IS NULL OR NOT ST_Covers(approved_geometry, NEW.boundary) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Active Zone must be covered by the approved FarmBoundary.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM farm.farm_zones other_zone
                        WHERE other_zone.farm_id = NEW.farm_id
                          AND other_zone.id <> NEW.id
                          AND other_zone.deleted_at IS NULL
                          AND other_zone.status = 'ACTIVE'::system.general_status
                          AND ST_Intersects(other_zone.boundary, NEW.boundary)
                          AND NOT ST_Touches(other_zone.boundary, NEW.boundary)
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Active Zone boundaries cannot overlap.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_farm_zones_enforce_approved_boundary
                BEFORE INSERT OR UPDATE OF farm_id, boundary, status, deleted_at ON farm.farm_zones
                FOR EACH ROW EXECUTE FUNCTION farm.enforce_zone_against_approved_boundary();

                CREATE OR REPLACE FUNCTION farm.enforce_boundary_exception_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'BoundaryException history cannot be deleted.';
                    END IF;

                    IF NEW.id <> OLD.id
                       OR NEW.tenant_id <> OLD.tenant_id
                       OR NEW.farm_id <> OLD.farm_id
                       OR NEW.farm_boundary_version_id <> OLD.farm_boundary_version_id
                       OR NEW.source <> OLD.source
                       OR NEW.source_reference_id <> OLD.source_reference_id
                       OR NEW.survey_order_id IS DISTINCT FROM OLD.survey_order_id
                       OR NEW.mission_id IS DISTINCT FROM OLD.mission_id
                       OR NOT ST_Equals(NEW.original_position, OLD.original_position)
                       OR NEW.measured_distance_meters <> OLD.measured_distance_meters
                       OR NEW.threshold_meters <> OLD.threshold_meters
                       OR NEW.policy_version <> OLD.policy_version THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'BoundaryException source facts and original position are immutable.';
                    END IF;

                    IF OLD.state = 'RESOLVED'::system.boundary_exception_state THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Resolved BoundaryException history is immutable.';
                    END IF;

                    IF NEW.state NOT IN (OLD.state, 'RESOLVED'::system.boundary_exception_state) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Invalid BoundaryException state transition.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_boundary_exceptions_protect_history
                BEFORE UPDATE OR DELETE ON farm.boundary_exceptions
                FOR EACH ROW EXECUTE FUNCTION farm.enforce_boundary_exception_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM farm.boundary_exceptions)
                       OR EXISTS (
                           SELECT 1
                           FROM farm.farm_boundaries
                           WHERE source <> 'LEGACY_IMPORT'::system.farm_boundary_source
                              OR status <> 'DRAFT'::system.farm_boundary_status
                       )
                       OR EXISTS (
                           SELECT 1
                           FROM survey.survey_orders
                           WHERE farm_boundary_version_id IS NOT NULL
                       ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = 'P0001',
                            MESSAGE = '0R-3 rollback blocked: target FarmBoundary or BoundaryException data exists.';
                    END IF;
                END $$;

                DROP TRIGGER IF EXISTS tr_boundary_exceptions_protect_history ON farm.boundary_exceptions;
                DROP FUNCTION IF EXISTS farm.enforce_boundary_exception_history();
                DROP TRIGGER IF EXISTS tr_farm_zones_enforce_approved_boundary ON farm.farm_zones;
                DROP FUNCTION IF EXISTS farm.enforce_zone_against_approved_boundary();
                DROP TRIGGER IF EXISTS tr_farm_boundaries_validate_zones ON farm.farm_boundaries;
                DROP FUNCTION IF EXISTS farm.validate_approved_boundary_zones();
                DROP TRIGGER IF EXISTS tr_farm_boundaries_protect_history ON farm.farm_boundaries;
                DROP FUNCTION IF EXISTS farm.enforce_farm_boundary_history();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_survey_orders_farm_boundary_same_tenant_farm",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropTable(
                name: "boundary_exceptions",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "farm_boundaries",
                schema: "farm");

            migrationBuilder.DropIndex(
                name: "IX_survey_orders_farm_boundary_version_id_tenant_id_farm_id",
                schema: "survey",
                table: "survey_orders");

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
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:system.boundary_exception_decision", "ACCEPTED_INSIDE,REJECTED_OUTSIDE,LOCATION_CORRECTED")
                .OldAnnotation("Npgsql:Enum:system.boundary_exception_source", "BASELINE_CANDIDATE,HEALTH_OBSERVATION,PLANT_INVENTORY_CHANGE,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.boundary_exception_state", "OUT_OF_BOUNDARY,NEEDS_REVIEW,RESOLVED")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,LEGACY_IMPORT")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
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
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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
