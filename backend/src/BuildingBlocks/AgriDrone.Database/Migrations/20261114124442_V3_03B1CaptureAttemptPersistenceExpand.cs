using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_03B1CaptureAttemptPersistenceExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These labels were already present in the committed model snapshot after 03A,
            // but no historical migration owns them. Keep the expand migration explicit and
            // idempotent for both fresh databases and populated upgrades.
            migrationBuilder.Sql(
                """
                ALTER TYPE system.survey_appointment_purpose ADD VALUE IF NOT EXISTS 'CAPTURE';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_APPOINTMENT';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_DEPOSIT';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'READY_FOR_CAPTURE';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'CAPTURING';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_CAPTURE_UPLOAD';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_INPUT_REVIEW';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_PROCESSING';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'PROCESSING';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_REFLIGHT_DECISION';
                ALTER TYPE system.survey_order_status ADD VALUE IF NOT EXISTS 'AWAITING_SETTLEMENT';
                """,
                suppressTransaction: true);

            migrationBuilder.DropIndex(
                name: "uq_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "RELATIVE_TO_TAKEOFF,AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.boundary_exception_decision", "ACCEPTED_INSIDE,REJECTED_OUTSIDE,LOCATION_CORRECTED")
                .Annotation("Npgsql:Enum:system.boundary_exception_source", "BASELINE_CANDIDATE,HEALTH_OBSERVATION,PLANT_INVENTORY_CHANGE,MANUAL")
                .Annotation("Npgsql:Enum:system.boundary_exception_state", "OUT_OF_BOUNDARY,NEEDS_REVIEW,RESOLVED")
                .Annotation("Npgsql:Enum:system.capture_attempt_status", "PLANNED,PREFLIGHT_READY,IN_FLIGHT,AWAITING_UPLOAD,UPLOADED,PROCESSING,ACCEPTED,REFLIGHT_REQUIRED,FLIGHT_FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.capture_input_manifest_status", "UPLOADED,SUPERSEDED,APPROVED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.contract_scope", "LEGACY,V3")
                .Annotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .Annotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,SYSTEM_MANAGER,LEGACY_IMPORT")
                .Annotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_readiness_criterion_status", "EXPERIMENTAL,VALIDATED,RETIRED")
                .Annotation("Npgsql:Enum:system.harvest_readiness_granularity", "PLANT,FARM")
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
                .Annotation("Npgsql:Enum:system.plant_inventory_change_kind", "REMOVED,REPLACED,NEW_PLANT")
                .Annotation("Npgsql:Enum:system.plant_inventory_change_status", "SUBMITTED,UNDER_REVIEW,AWAITING_SURVEY_EVIDENCE,VERIFIED,APPLIED,REJECTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE,CAPTURE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .Annotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .Annotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .Annotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .Annotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .Annotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .Annotation("Npgsql:Enum:system.survey_scope_type", "FARM_WIDE,ZONES")
                .Annotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .Annotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .Annotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .Annotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.treatment_recommendation_status", "DRAFT,PUBLISHED,RETIRED,SUPERSEDED")
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
                .OldAnnotation("Npgsql:Enum:system.contract_scope", "LEGACY,V3")
                .OldAnnotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .OldAnnotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,SYSTEM_MANAGER,LEGACY_IMPORT")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_readiness_criterion_status", "EXPERIMENTAL,VALIDATED,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.harvest_readiness_granularity", "PLANT,FARM")
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
                .OldAnnotation("Npgsql:Enum:system.plant_inventory_change_kind", "REMOVED,REPLACED,NEW_PLANT")
                .OldAnnotation("Npgsql:Enum:system.plant_inventory_change_status", "SUBMITTED,UNDER_REVIEW,AWAITING_SURVEY_EVIDENCE,VERIFIED,APPLIED,REJECTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE,CAPTURE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .OldAnnotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .OldAnnotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .OldAnnotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .OldAnnotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.survey_scope_type", "FARM_WIDE,ZONES")
                .OldAnnotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .OldAnnotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .OldAnnotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.treatment_recommendation_status", "DRAFT,PUBLISHED,RETIRED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "deposit_obligation_id",
                schema: "survey",
                table: "survey_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "transition_sequence",
                schema: "survey",
                table: "order_state_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "capture_attempt_id",
                schema: "mission",
                table: "drone_missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "capture_attempts",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_index = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<CaptureAttemptStatus>(type: "system.capture_attempt_status", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    parent_mission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reflight_approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reflight_approval_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reflight_approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    preflight_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    flight_completion_receipt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    flight_failure_evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    upload_manifest_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    upload_manifest_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    input_approval_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processing_job_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processing_acceptance_receipt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reflight_decision_evidence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancellation_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_predecessor_history_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_attempts", x => x.id);
                    table.UniqueConstraint("uq_capture_attempts_id_owner", x => new { x.id, x.tenant_id, x.farm_id, x.order_id });
                    table.CheckConstraint("ck_capture_attempts_flight_time", "ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at");
                    table.CheckConstraint("ck_capture_attempts_index", "attempt_index >= 1");
                    table.CheckConstraint("ck_capture_attempts_successor_approval", "(superseded_attempt_id IS NULL AND reflight_approved_by IS NULL AND reflight_approval_reason IS NULL AND reflight_approved_at IS NULL) OR (superseded_attempt_id IS NOT NULL AND reflight_approved_by IS NOT NULL AND reflight_approval_reason IS NOT NULL AND reflight_approved_at IS NOT NULL)");
                    table.CheckConstraint("ck_capture_attempts_updated_at", "updated_at >= created_at");
                    table.CheckConstraint("ck_capture_attempts_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_capture_attempts_order_same_owner",
                        columns: x => new { x.order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_attempts_parent_same_owner",
                        columns: x => new { x.superseded_attempt_id, x.tenant_id, x.farm_id, x.order_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_attempts_users_reflight_approved_by",
                        column: x => x.reflight_approved_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capture_attempt_history",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transition_sequence = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    from_status = table.Column<CaptureAttemptStatus>(type: "system.capture_attempt_status", nullable: true),
                    to_status = table.Column<CaptureAttemptStatus>(type: "system.capture_attempt_status", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    predecessor_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_attempt_history", x => x.id);
                    table.UniqueConstraint("uq_capture_attempt_history_id_attempt", x => new { x.id, x.capture_attempt_id });
                    table.CheckConstraint("ck_capture_attempt_history_sequence", "transition_sequence >= 1");
                    table.ForeignKey(
                        name: "fk_capture_attempt_history_attempt",
                        column: x => x.capture_attempt_id,
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_attempt_history_predecessor_same_attempt",
                        columns: x => new { x.predecessor_history_id, x.capture_attempt_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempt_history",
                        principalColumns: new[] { "id", "capture_attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_attempt_history_users_actor",
                        column: x => x.actor_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capture_input_manifests",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    total_file_count = table.Column<int>(type: "integer", nullable: false),
                    total_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    gps_metadata_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<CaptureInputManifestStatus>(type: "system.capture_input_manifest_status", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_input_manifests", x => x.id);
                    table.UniqueConstraint("uq_capture_input_manifests_identity", x => new { x.id, x.tenant_id, x.farm_id, x.survey_order_id, x.capture_attempt_id, x.mission_id, x.manifest_fingerprint });
                    table.CheckConstraint("ck_capture_input_manifests_evaluated", "evaluated_at IS NULL OR evaluated_at >= created_at");
                    table.CheckConstraint("ck_capture_input_manifests_file_count", "total_file_count > 0");
                    table.CheckConstraint("ck_capture_input_manifests_size", "total_size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_capture_input_manifests_attempt_same_owner",
                        columns: x => new { x.capture_attempt_id, x.tenant_id, x.farm_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "capture_attempts",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_input_manifests_mission",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capture_input_approvals",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    processing_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    technical_quality_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capture_input_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_capture_input_approvals_manifest_identity",
                        columns: x => new { x.input_manifest_id, x.tenant_id, x.farm_id, x.survey_order_id, x.capture_attempt_id, x.mission_id, x.manifest_fingerprint },
                        principalSchema: "survey",
                        principalTable: "capture_input_manifests",
                        principalColumns: new[] { "id", "tenant_id", "farm_id", "survey_order_id", "capture_attempt_id", "mission_id", "manifest_fingerprint" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_input_approvals_mission",
                        column: x => x.mission_id,
                        principalSchema: "mission",
                        principalTable: "drone_missions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_input_approvals_processing_job",
                        column: x => x.processing_job_id,
                        principalSchema: "mission",
                        principalTable: "ai_processing_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capture_input_approvals_users_approved_by",
                        column: x => x.approved_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_survey_orders_deposit_obligation",
                schema: "survey",
                table: "survey_orders",
                column: "deposit_obligation_id",
                unique: true,
                filter: "deposit_obligation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_order_state_history_transition_sequence",
                schema: "survey",
                table: "order_state_history",
                columns: new[] { "survey_order_id", "transition_sequence" },
                unique: true,
                filter: "transition_sequence IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_state_history_transition_sequence",
                schema: "survey",
                table: "order_state_history",
                sql: "transition_sequence IS NULL OR transition_sequence >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_drone_missions_capture_attempt_owner",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_drone_missions_capture_attempt",
                schema: "mission",
                table: "drone_missions",
                column: "capture_attempt_id",
                unique: true,
                filter: "capture_attempt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "survey_order_id", "mission_purpose" },
                unique: true,
                filter: "survey_order_id IS NOT NULL AND capture_attempt_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_drone_missions_capture_attempt_order",
                schema: "mission",
                table: "drone_missions",
                sql: "capture_attempt_id IS NULL OR survey_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempt_history_actor",
                schema: "survey",
                table: "capture_attempt_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempt_history_correlation",
                schema: "survey",
                table: "capture_attempt_history",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempt_history_predecessor",
                schema: "survey",
                table: "capture_attempt_history",
                columns: new[] { "predecessor_history_id", "capture_attempt_id" });

            migrationBuilder.CreateIndex(
                name: "uq_capture_attempt_history_operation",
                schema: "survey",
                table: "capture_attempt_history",
                columns: new[] { "capture_attempt_id", "operation_id" },
                unique: true,
                filter: "operation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_capture_attempt_history_sequence",
                schema: "survey",
                table: "capture_attempt_history",
                columns: new[] { "capture_attempt_id", "transition_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempts_order_owner",
                schema: "survey",
                table: "capture_attempts",
                columns: new[] { "order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempts_parent_owner",
                schema: "survey",
                table: "capture_attempts",
                columns: new[] { "superseded_attempt_id", "tenant_id", "farm_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_attempts_reflight_approved_by",
                schema: "survey",
                table: "capture_attempts",
                column: "reflight_approved_by");

            migrationBuilder.CreateIndex(
                name: "uq_capture_attempts_active_order",
                schema: "survey",
                table: "capture_attempts",
                column: "order_id",
                unique: true,
                filter: "status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING')");

            migrationBuilder.CreateIndex(
                name: "uq_capture_attempts_order_index",
                schema: "survey",
                table: "capture_attempts",
                columns: new[] { "order_id", "attempt_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_capture_attempts_parent_successor",
                schema: "survey",
                table: "capture_attempts",
                column: "superseded_attempt_id",
                unique: true,
                filter: "superseded_attempt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_approvals_approved_by",
                schema: "survey",
                table: "capture_input_approvals",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_approvals_manifest_identity",
                schema: "survey",
                table: "capture_input_approvals",
                columns: new[] { "input_manifest_id", "tenant_id", "farm_id", "survey_order_id", "capture_attempt_id", "mission_id", "manifest_fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_approvals_mission",
                schema: "survey",
                table: "capture_input_approvals",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "uq_capture_input_approvals_manifest",
                schema: "survey",
                table: "capture_input_approvals",
                column: "input_manifest_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_capture_input_approvals_processing_job",
                schema: "survey",
                table: "capture_input_approvals",
                column: "processing_job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_manifests_attempt_owner",
                schema: "survey",
                table: "capture_input_manifests",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_capture_input_manifests_mission",
                schema: "survey",
                table: "capture_input_manifests",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "uq_capture_input_manifests_attempt_fingerprint",
                schema: "survey",
                table: "capture_input_manifests",
                columns: new[] { "capture_attempt_id", "manifest_fingerprint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_drone_missions_capture_attempt_same_owner",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "capture_attempt_id", "tenant_id", "farm_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "capture_attempts",
                principalColumns: new[] { "id", "tenant_id", "farm_id", "order_id" },
                onDelete: ReferentialAction.Restrict);

            // The cross-schema Mission FK proves owner/Order alignment. These constraint
            // triggers additionally prove that evidence references the exact Mission bound
            // to the same Attempt, preventing a same-Order cross-attempt link.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION survey.enforce_capture_attempt_v3_scope()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM survey.survey_orders AS survey_order
                        WHERE survey_order.id = NEW.order_id
                          AND survey_order.tenant_id = NEW.tenant_id
                          AND survey_order.farm_id = NEW.farm_id
                          AND survey_order.scope = 'V3'::system.contract_scope
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            CONSTRAINT = 'ck_capture_attempts_v3_order_scope',
                            MESSAGE = 'Capture attempts can only belong to a same-owner V3 Order.';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER ck_capture_attempts_v3_order_scope
                AFTER INSERT OR UPDATE ON survey.capture_attempts
                DEFERRABLE INITIALLY IMMEDIATE
                FOR EACH ROW
                EXECUTE FUNCTION survey.enforce_capture_attempt_v3_scope();

                CREATE FUNCTION survey.enforce_capture_evidence_mission_binding()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM mission.drone_missions AS mission
                        WHERE mission.id = NEW.mission_id
                          AND mission.capture_attempt_id = NEW.capture_attempt_id
                          AND mission.tenant_id = NEW.tenant_id
                          AND mission.farm_id = NEW.farm_id
                          AND mission.survey_order_id = NEW.survey_order_id
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            CONSTRAINT = 'ck_capture_evidence_mission_binding',
                            MESSAGE = 'Capture evidence must reference the Mission bound to the same Attempt and owner.';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER ck_capture_input_manifests_mission_binding
                AFTER INSERT OR UPDATE ON survey.capture_input_manifests
                DEFERRABLE INITIALLY IMMEDIATE
                FOR EACH ROW
                EXECUTE FUNCTION survey.enforce_capture_evidence_mission_binding();

                CREATE CONSTRAINT TRIGGER ck_capture_input_approvals_mission_binding
                AFTER INSERT OR UPDATE ON survey.capture_input_approvals
                DEFERRABLE INITIALLY IMMEDIATE
                FOR EACH ROW
                EXECUTE FUNCTION survey.enforce_capture_evidence_mission_binding();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Durable evidence/history and linked operational rows are roll-forward only.
            // A down migration is allowed solely while every expand artifact is empty.
            migrationBuilder.Sql(
                """
                DO $block$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.capture_attempts)
                       OR EXISTS (SELECT 1 FROM survey.survey_orders WHERE deposit_obligation_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM survey.order_state_history WHERE transition_sequence IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM mission.drone_missions WHERE capture_attempt_id IS NOT NULL)
                    THEN
                        RAISE EXCEPTION
                            'V3 03B-1 persistence contains durable data; roll forward instead of migrating down.';
                    END IF;
                END;
                $block$;

                DROP TRIGGER IF EXISTS ck_capture_input_approvals_mission_binding
                    ON survey.capture_input_approvals;
                DROP TRIGGER IF EXISTS ck_capture_input_manifests_mission_binding
                    ON survey.capture_input_manifests;
                DROP TRIGGER IF EXISTS ck_capture_attempts_v3_order_scope
                    ON survey.capture_attempts;
                DROP FUNCTION IF EXISTS survey.enforce_capture_evidence_mission_binding();
                DROP FUNCTION IF EXISTS survey.enforce_capture_attempt_v3_scope();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_drone_missions_capture_attempt_same_owner",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropTable(
                name: "capture_attempt_history",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "capture_input_approvals",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "capture_input_manifests",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "capture_attempts",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "uq_survey_orders_deposit_obligation",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropIndex(
                name: "uq_order_state_history_transition_sequence",
                schema: "survey",
                table: "order_state_history");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_state_history_transition_sequence",
                schema: "survey",
                table: "order_state_history");

            migrationBuilder.DropIndex(
                name: "ix_drone_missions_capture_attempt_owner",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "uq_drone_missions_capture_attempt",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropIndex(
                name: "uq_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_capture_attempt_order",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "deposit_obligation_id",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "transition_sequence",
                schema: "survey",
                table: "order_state_history");

            migrationBuilder.DropColumn(
                name: "capture_attempt_id",
                schema: "mission",
                table: "drone_missions");

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
                .Annotation("Npgsql:Enum:system.contract_scope", "LEGACY,V3")
                .Annotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .Annotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,SYSTEM_MANAGER,LEGACY_IMPORT")
                .Annotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_readiness_criterion_status", "EXPERIMENTAL,VALIDATED,RETIRED")
                .Annotation("Npgsql:Enum:system.harvest_readiness_granularity", "PLANT,FARM")
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
                .Annotation("Npgsql:Enum:system.plant_inventory_change_kind", "REMOVED,REPLACED,NEW_PLANT")
                .Annotation("Npgsql:Enum:system.plant_inventory_change_status", "SUBMITTED,UNDER_REVIEW,AWAITING_SURVEY_EVIDENCE,VERIFIED,APPLIED,REJECTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .Annotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .Annotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .Annotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .Annotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .Annotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .Annotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE,CAPTURE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .Annotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .Annotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .Annotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .Annotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .Annotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .Annotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .Annotation("Npgsql:Enum:system.survey_scope_type", "FARM_WIDE,ZONES")
                .Annotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .Annotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .Annotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .Annotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .Annotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .Annotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.treatment_recommendation_status", "DRAFT,PUBLISHED,RETIRED,SUPERSEDED")
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
                .OldAnnotation("Npgsql:Enum:system.capture_attempt_status", "PLANNED,PREFLIGHT_READY,IN_FLIGHT,AWAITING_UPLOAD,UPLOADED,PROCESSING,ACCEPTED,REFLIGHT_REQUIRED,FLIGHT_FAILED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.capture_input_manifest_status", "UPLOADED,SUPERSEDED,APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.contract_scope", "LEGACY,V3")
                .OldAnnotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .OldAnnotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,SYSTEM_MANAGER,LEGACY_IMPORT")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_readiness_criterion_status", "EXPERIMENTAL,VALIDATED,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.harvest_readiness_granularity", "PLANT,FARM")
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
                .OldAnnotation("Npgsql:Enum:system.plant_inventory_change_kind", "REMOVED,REPLACED,NEW_PLANT")
                .OldAnnotation("Npgsql:Enum:system.plant_inventory_change_status", "SUBMITTED,UNDER_REVIEW,AWAITING_SURVEY_EVIDENCE,VERIFIED,APPLIED,REJECTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:system.plant_lifecycle_status", "ACTIVE,MISSING,REMOVED,DEAD,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.position_source", "MAPPING_AI,MANUAL,IMPORT")
                .OldAnnotation("Npgsql:Enum:system.preflight_checklist_definition_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.price_adjustment_status", "PENDING,APPROVED,REJECTED,APPLIED")
                .OldAnnotation("Npgsql:Enum:system.processing_status", "NOT_UPLOADED,UPLOADED,QUEUED,PROCESSING,COMPLETED,FAILED,REVIEW_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.review_status", "PENDING,CONFIRMED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.scan_media_role", "PRIMARY,CONTEXT,DETECTION_RESULT")
                .OldAnnotation("Npgsql:Enum:system.scan_source", "DRONE_AI,FIELD_MANUAL,MANAGER")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE,CAPTURE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .OldAnnotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .OldAnnotation("Npgsql:Enum:system.survey_request_kind", "NEW_CUSTOMER,EXISTING_TENANT_NEW_FARM,EXISTING_FARM_SURVEY")
                .OldAnnotation("Npgsql:Enum:system.survey_request_status", "SUBMITTED,UNDER_REVIEW,APPROVED,REJECTED,WITHDRAWN")
                .OldAnnotation("Npgsql:Enum:system.survey_result_status", "PENDING_REVIEW,APPROVED,PUBLISHED")
                .OldAnnotation("Npgsql:Enum:system.survey_review_decision", "APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.survey_scope_type", "FARM_WIDE,ZONES")
                .OldAnnotation("Npgsql:Enum:system.survey_service_status", "EXPERIMENTAL,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_service_type", "PLANT_HEALTH,HARVEST_READINESS")
                .OldAnnotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .OldAnnotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_purpose", "MEMBERSHIP,OWNER_PROVISIONING")
                .OldAnnotation("Npgsql:Enum:system.tenant_invitation_status", "PENDING,ACCEPTED,REVOKED,EXPIRED")
                .OldAnnotation("Npgsql:Enum:system.tenant_member_role", "OWNER,TENANT_ADMIN,MEMBER")
                .OldAnnotation("Npgsql:Enum:system.threshold_profile_status", "DRAFT,ACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.treatment_recommendation_status", "DRAFT,PUBLISHED,RETIRED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateIndex(
                name: "uq_drone_missions_order_purpose",
                schema: "mission",
                table: "drone_missions",
                columns: new[] { "survey_order_id", "mission_purpose" },
                unique: true,
                filter: "survey_order_id IS NOT NULL");
        }
    }
}
