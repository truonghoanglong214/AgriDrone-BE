using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class V3_03B2FundingCancellationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_purpose",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.Sql(
                """
                DO $enum_expand$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_type type
                        JOIN pg_namespace ns ON ns.oid = type.typnamespace
                        WHERE ns.nspname = 'system' AND type.typname = 'cancellation_decision_kind')
                    THEN
                        CREATE TYPE system.cancellation_decision_kind AS ENUM
                            ('CUSTOMER_BEFORE_CAPTURE', 'CUSTOMER_AFTER_ACCEPTABLE_CAPTURE', 'OPERATIONAL_POSTPONEMENT', 'SERVICE_FAILURE');
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_type type
                        JOIN pg_namespace ns ON ns.oid = type.typnamespace
                        WHERE ns.nspname = 'system' AND type.typname = 'survey_deposit_obligation_status')
                    THEN
                        CREATE TYPE system.survey_deposit_obligation_status AS ENUM
                            ('PENDING', 'SATISFIED', 'CANCELLED');
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_type type
                        JOIN pg_namespace ns ON ns.oid = type.typnamespace
                        WHERE ns.nspname = 'system' AND type.typname = 'survey_refund_obligation_status')
                    THEN
                        CREATE TYPE system.survey_refund_obligation_status AS ENUM
                            ('PENDING', 'RESERVED', 'EXECUTING', 'COMPLETED', 'CANCELLED');
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_type type
                        JOIN pg_namespace ns ON ns.oid = type.typnamespace
                        WHERE ns.nspname = 'system' AND type.typname = 'survey_refund_origin_kind')
                    THEN
                        CREATE TYPE system.survey_refund_origin_kind AS ENUM
                            ('CANCELLATION_RETURN', 'SERVICE_FAILURE', 'OVERPAYMENT', 'COMPLAINT_REFUND');
                    END IF;
                END
                $enum_expand$;
                ALTER TYPE system.mission_purpose ADD VALUE IF NOT EXISTS 'CAPTURE';
                """,
                suppressTransaction: true);

            // Enum expansion is emitted explicitly above so re-upgrade remains
            // idempotent after a safe schema rollback retains CAPTURE.
            if (migrationBuilder.ActiveProvider == "__generated_enum_expansion_disabled__")
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
                .Annotation("Npgsql:Enum:system.cancellation_decision_kind", "CUSTOMER_BEFORE_CAPTURE,CUSTOMER_AFTER_ACCEPTABLE_CAPTURE,OPERATIONAL_POSTPONEMENT,SERVICE_FAILURE")
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
                .Annotation("Npgsql:Enum:system.mission_purpose", "BASELINE_MAPPING,PLANT_HEALTH,HARVEST_READINESS,CAPTURE")
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
                .Annotation("Npgsql:Enum:system.survey_deposit_obligation_status", "PENDING,SATISFIED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .Annotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .Annotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .Annotation("Npgsql:Enum:system.survey_refund_obligation_status", "PENDING,RESERVED,EXECUTING,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.survey_refund_origin_kind", "CANCELLATION_RETURN,SERVICE_FAILURE,OVERPAYMENT,COMPLAINT_REFUND")
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
            }

            migrationBuilder.AddColumn<Guid>(
                name: "deposit_obligation_id",
                schema: "survey",
                table: "survey_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "survey_cancellation_decisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_version = table.Column<uint>(type: "xid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_policy_version = table.Column<int>(type: "integer", nullable: false),
                    decision_kind = table.Column<CancellationDecisionKind>(type: "system.cancellation_decision_kind", nullable: false),
                    refundable_before_capture = table.Column<bool>(type: "boolean", nullable: false),
                    retain_deposit_after_capture = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_free_no_show_reschedules = table.Column<int>(type: "integer", nullable: false),
                    operational_postponement_refundable = table.Column<bool>(type: "boolean", nullable: false),
                    service_failure_refundable = table.Column<bool>(type: "boolean", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    input_approval_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_receipts_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    completed_refunds_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    active_refund_reservations_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pinned_deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    retained_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    predecessor_decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_cancellation_decisions", x => x.id);
                    table.UniqueConstraint("uq_survey_cancellation_decisions_id_order", x => new { x.id, x.survey_order_id });
                    table.CheckConstraint("ck_survey_cancellation_decisions_acceptable_capture", "decision_kind <> 'CUSTOMER_AFTER_ACCEPTABLE_CAPTURE'::system.cancellation_decision_kind OR input_approval_evidence_id IS NOT NULL");
                    table.CheckConstraint("ck_survey_cancellation_decisions_amounts", "confirmed_receipts_amount >= 0 AND completed_refunds_amount >= 0 AND active_refund_reservations_amount >= 0 AND pinned_deposit_amount >= 0 AND retained_amount >= 0 AND refund_amount >= 0 AND confirmed_receipts_amount = FLOOR(confirmed_receipts_amount) AND completed_refunds_amount = FLOOR(completed_refunds_amount) AND active_refund_reservations_amount = FLOOR(active_refund_reservations_amount) AND pinned_deposit_amount = FLOOR(pinned_deposit_amount) AND retained_amount = FLOOR(retained_amount) AND refund_amount = FLOOR(refund_amount)");
                    table.CheckConstraint("ck_survey_cancellation_decisions_arithmetic", "confirmed_receipts_amount >= completed_refunds_amount + active_refund_reservations_amount AND retained_amount <= LEAST(pinned_deposit_amount, confirmed_receipts_amount - completed_refunds_amount - active_refund_reservations_amount) AND refund_amount = confirmed_receipts_amount - completed_refunds_amount - active_refund_reservations_amount - retained_amount");
                    table.CheckConstraint("ck_survey_cancellation_decisions_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_cancellation_decisions_free_reschedules", "allowed_free_no_show_reschedules >= 0");
                    table.CheckConstraint("ck_survey_cancellation_decisions_policy_version", "cancellation_policy_version >= 1");
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_input_approval",
                        column: x => x.input_approval_evidence_id,
                        principalSchema: "survey",
                        principalTable: "capture_input_approvals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_policy",
                        column: x => x.cancellation_policy_id,
                        principalSchema: "survey",
                        principalTable: "cancellation_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_predecessor_order",
                        columns: x => new { x.predecessor_decision_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_cancellation_decisions",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_users_actor",
                        column: x => x.decision_actor_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_cancellation_decisions_users_requester",
                        column: x => x.requester_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_deposit_obligations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<SurveyDepositObligationStatus>(type: "system.survey_deposit_obligation_status", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    satisfied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_deposit_obligations", x => x.id);
                    table.UniqueConstraint("uq_survey_deposit_obligations_id_order", x => new { x.id, x.survey_order_id });
                    table.CheckConstraint("ck_survey_deposit_obligations_allocated", "allocated_amount >= 0 AND allocated_amount = FLOOR(allocated_amount)");
                    table.CheckConstraint("ck_survey_deposit_obligations_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_survey_deposit_obligations_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_deposit_obligations_status_time", "(status = 'SATISFIED'::system.survey_deposit_obligation_status AND satisfied_at IS NOT NULL) OR (status = 'CANCELLED'::system.survey_deposit_obligation_status AND cancelled_at IS NOT NULL) OR (status = 'PENDING'::system.survey_deposit_obligation_status AND satisfied_at IS NULL AND cancelled_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_survey_deposit_obligations_business_policy",
                        column: x => x.business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_deposit_obligations_deposit_policy",
                        column: x => x.deposit_policy_id,
                        principalSchema: "survey",
                        principalTable: "v3_survey_deposit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_deposit_obligations_order_same_owner",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cancellation_retentions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cancellation_retentions", x => x.id);
                    table.CheckConstraint("ck_cancellation_retentions_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_cancellation_retentions_currency", "currency = 'VND'");
                    table.ForeignKey(
                        name: "fk_cancellation_retentions_decision_order",
                        columns: x => new { x.cancellation_decision_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_cancellation_decisions",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_funding_receipts",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_obligation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    payload_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_funding_receipts", x => x.id);
                    table.UniqueConstraint("uq_survey_funding_receipts_id_order", x => new { x.id, x.survey_order_id });
                    table.CheckConstraint("ck_survey_funding_receipts_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_survey_funding_receipts_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_funding_receipts_received_at", "created_at >= received_at");
                    table.ForeignKey(
                        name: "fk_survey_funding_receipts_deposit_order",
                        columns: x => new { x.deposit_obligation_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_deposit_obligations",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_funding_receipts_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_funding_allocations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    allocated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_funding_allocations", x => x.id);
                    table.CheckConstraint("ck_survey_funding_allocations_amount", "allocated_amount > 0 AND allocated_amount = FLOOR(allocated_amount)");
                    table.CheckConstraint("ck_survey_funding_allocations_currency", "currency = 'VND'");
                    table.ForeignKey(
                        name: "fk_survey_funding_allocations_obligation_order",
                        columns: x => new { x.deposit_obligation_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_deposit_obligations",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_funding_allocations_receipt_order",
                        columns: x => new { x.funding_receipt_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_funding_receipts",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_refund_obligations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_kind = table.Column<SurveyRefundOriginKind>(type: "system.survey_refund_origin_kind", nullable: false),
                    origin_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    status = table.Column<SurveyRefundObligationStatus>(type: "system.survey_refund_obligation_status", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reserved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executing_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_refund_obligations", x => x.id);
                    table.CheckConstraint("ck_survey_refund_obligations_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_survey_refund_obligations_currency", "currency = 'VND'");
                    table.CheckConstraint("ck_survey_refund_obligations_status_time", "(status = 'PENDING'::system.survey_refund_obligation_status) OR (status = 'RESERVED'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL) OR (status = 'EXECUTING'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL AND executing_at IS NOT NULL) OR (status = 'COMPLETED'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL AND executing_at IS NOT NULL AND completed_at IS NOT NULL) OR (status = 'CANCELLED'::system.survey_refund_obligation_status AND cancelled_at IS NOT NULL)");
                    table.CheckConstraint("ck_survey_refund_obligations_updated_at", "updated_at >= created_at");
                    table.ForeignKey(
                        name: "fk_survey_refund_obligations_decision_order",
                        columns: x => new { x.origin_decision_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_cancellation_decisions",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_refund_obligations_order",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_refund_obligations_receipt_order",
                        columns: x => new { x.funding_receipt_id, x.survey_order_id },
                        principalSchema: "survey",
                        principalTable: "survey_funding_receipts",
                        principalColumns: new[] { "id", "survey_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_payments_deposit_obligation_id_survey_order_id",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "deposit_obligation_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_payments_active_deposit_obligation",
                schema: "survey",
                table: "survey_payments",
                column: "deposit_obligation_id",
                unique: true,
                filter: "deposit_obligation_id IS NOT NULL AND status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'PAID'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_deposit_obligation",
                schema: "survey",
                table: "survey_payments",
                sql: "(purpose = 'OrderDeposit' AND deposit_obligation_id IS NOT NULL) OR (purpose <> 'OrderDeposit' AND deposit_obligation_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_purpose",
                schema: "survey",
                table: "survey_payments",
                sql: "purpose IN ('BaselineDeposit', 'FinalBalance', 'OrderDeposit')");

            migrationBuilder.CreateIndex(
                name: "IX_cancellation_retentions_cancellation_decision_id_survey_ord~",
                schema: "survey",
                table: "cancellation_retentions",
                columns: new[] { "cancellation_decision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cancellation_retentions_order",
                schema: "survey",
                table: "cancellation_retentions",
                column: "survey_order_id");

            migrationBuilder.CreateIndex(
                name: "uq_cancellation_retentions_decision",
                schema: "survey",
                table: "cancellation_retentions",
                column: "cancellation_decision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_cancellation_decisions_cancellation_policy_id",
                schema: "survey",
                table: "survey_cancellation_decisions",
                column: "cancellation_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_cancellation_decisions_decision_actor_id",
                schema: "survey",
                table: "survey_cancellation_decisions",
                column: "decision_actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_cancellation_decisions_input_approval_evidence_id",
                schema: "survey",
                table: "survey_cancellation_decisions",
                column: "input_approval_evidence_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_cancellation_decisions_predecessor_decision_id_surve~",
                schema: "survey",
                table: "survey_cancellation_decisions",
                columns: new[] { "predecessor_decision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_cancellation_decisions_requester_id",
                schema: "survey",
                table: "survey_cancellation_decisions",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_cancellation_decisions_order_operation",
                schema: "survey",
                table: "survey_cancellation_decisions",
                columns: new[] { "survey_order_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_survey_cancellation_decisions_predecessor",
                schema: "survey",
                table: "survey_cancellation_decisions",
                column: "predecessor_decision_id",
                unique: true,
                filter: "predecessor_decision_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_survey_deposit_obligations_business_policy_version_id",
                schema: "survey",
                table: "survey_deposit_obligations",
                column: "business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_deposit_obligations_deposit_policy_id",
                schema: "survey",
                table: "survey_deposit_obligations",
                column: "deposit_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_survey_deposit_obligations_order_owner",
                schema: "survey",
                table: "survey_deposit_obligations",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_deposit_obligations_order",
                schema: "survey",
                table: "survey_deposit_obligations",
                column: "survey_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_allocations_deposit_obligation_id_survey_ord~",
                schema: "survey",
                table: "survey_funding_allocations",
                columns: new[] { "deposit_obligation_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_allocations_funding_receipt_id_survey_order_~",
                schema: "survey",
                table: "survey_funding_allocations",
                columns: new[] { "funding_receipt_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_allocations_order_obligation",
                schema: "survey",
                table: "survey_funding_allocations",
                columns: new[] { "survey_order_id", "deposit_obligation_id", "allocated_at" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_funding_allocations_receipt_obligation",
                schema: "survey",
                table: "survey_funding_allocations",
                columns: new[] { "funding_receipt_id", "deposit_obligation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_receipts_deposit_obligation",
                schema: "survey",
                table: "survey_funding_receipts",
                column: "deposit_obligation_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_funding_receipts_deposit_obligation_id_survey_order_~",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "deposit_obligation_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_funding_receipts_order_received",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "survey_order_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_funding_receipts_provider_transaction",
                schema: "survey",
                table: "survey_funding_receipts",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_refund_obligations_funding_receipt_id_survey_order_id",
                schema: "survey",
                table: "survey_refund_obligations",
                columns: new[] { "funding_receipt_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_survey_refund_obligations_order_status",
                schema: "survey",
                table: "survey_refund_obligations",
                columns: new[] { "survey_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_refund_obligations_origin_decision_id_survey_order_id",
                schema: "survey",
                table: "survey_refund_obligations",
                columns: new[] { "origin_decision_id", "survey_order_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_refund_obligations_late_receipt",
                schema: "survey",
                table: "survey_refund_obligations",
                column: "funding_receipt_id",
                unique: true,
                filter: "funding_receipt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_survey_refund_obligations_origin",
                schema: "survey",
                table: "survey_refund_obligations",
                columns: new[] { "survey_order_id", "origin_kind", "origin_decision_id" },
                unique: true,
                filter: "funding_receipt_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_survey_payments_deposit_obligation_order",
                schema: "survey",
                table: "survey_payments",
                columns: new[] { "deposit_obligation_id", "survey_order_id" },
                principalSchema: "survey",
                principalTable: "survey_deposit_obligations",
                principalColumns: new[] { "id", "survey_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_orders
                ADD CONSTRAINT fk_survey_orders_deposit_obligation_order
                FOREIGN KEY (deposit_obligation_id, id)
                REFERENCES survey.survey_deposit_obligations (id, survey_order_id)
                ON DELETE RESTRICT
                DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.CreateCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions",
                sql: "(mission_purpose IS NULL AND ((mission_type = 'MAPPING'::system.mission_type AND source_map_version_id IS NULL) OR (mission_type = 'HEALTH_INSPECTION'::system.mission_type AND source_map_version_id IS NOT NULL))) OR (mission_purpose = 'BASELINE_MAPPING'::system.mission_purpose AND source_map_version_id IS NULL AND requires_baseline_completion = FALSE) OR (mission_purpose IN ('PLANT_HEALTH'::system.mission_purpose, 'HARVEST_READINESS'::system.mission_purpose) AND ((requires_baseline_completion = TRUE AND source_map_version_id IS NULL) OR (requires_baseline_completion = FALSE AND source_map_version_id IS NOT NULL))) OR (mission_purpose = 'CAPTURE'::system.mission_purpose AND requires_baseline_completion = FALSE)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM survey.survey_deposit_obligations)
                       OR EXISTS (SELECT 1 FROM survey.survey_funding_receipts)
                       OR EXISTS (SELECT 1 FROM survey.survey_funding_allocations)
                       OR EXISTS (SELECT 1 FROM survey.survey_cancellation_decisions)
                       OR EXISTS (SELECT 1 FROM survey.survey_refund_obligations)
                       OR EXISTS (SELECT 1 FROM survey.cancellation_retentions)
                       OR EXISTS (SELECT 1 FROM survey.survey_orders WHERE deposit_obligation_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM survey.survey_payments WHERE deposit_obligation_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM mission.drone_missions WHERE mission_purpose = 'CAPTURE'::system.mission_purpose)
                    THEN
                        RAISE EXCEPTION 'V3-03B2 downgrade refused: durable funding, cancellation, or Capture mission facts exist.';
                    END IF;
                END
                $migration_guard$;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE survey.survey_orders
                DROP CONSTRAINT fk_survey_orders_deposit_obligation_order;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_survey_payments_deposit_obligation_order",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropTable(
                name: "cancellation_retentions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_funding_allocations",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_refund_obligations",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_cancellation_decisions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_funding_receipts",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_deposit_obligations",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "IX_survey_payments_deposit_obligation_id_survey_order_id",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropIndex(
                name: "uq_survey_payments_active_deposit_obligation",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_deposit_obligation",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_payments_purpose",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.DropColumn(
                name: "deposit_obligation_id",
                schema: "survey",
                table: "survey_payments");

            migrationBuilder.Sql(
                """
                DROP TYPE system.cancellation_decision_kind;
                DROP TYPE system.survey_deposit_obligation_status;
                DROP TYPE system.survey_refund_obligation_status;
                DROP TYPE system.survey_refund_origin_kind;
                """);

            // PostgreSQL enum labels are expand-only. Contracting CAPTURE or the
            // V3 funding/cancellation enum labels is unsupported and unsafe even
            // after the owning tables are removed, so a schema rollback retains
            // those inert labels for a later re-upgrade.
            if (migrationBuilder.ActiveProvider == "__unsupported_enum_contraction__")
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
                .OldAnnotation("Npgsql:Enum:system.cancellation_decision_kind", "CUSTOMER_BEFORE_CAPTURE,CUSTOMER_AFTER_ACCEPTABLE_CAPTURE,OPERATIONAL_POSTPONEMENT,SERVICE_FAILURE")
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
                .OldAnnotation("Npgsql:Enum:system.mission_purpose", "BASELINE_MAPPING,PLANT_HEALTH,HARVEST_READINESS,CAPTURE")
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
                .OldAnnotation("Npgsql:Enum:system.survey_deposit_obligation_status", "PENDING,SATISFIED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED,AWAITING_APPOINTMENT,AWAITING_DEPOSIT,READY_FOR_CAPTURE,CAPTURING,AWAITING_CAPTURE_UPLOAD,AWAITING_INPUT_REVIEW,AWAITING_PROCESSING,PROCESSING,AWAITING_REFLIGHT_DECISION,AWAITING_SETTLEMENT")
                .OldAnnotation("Npgsql:Enum:system.survey_payment_status", "PENDING,PROCESSING,PAID,FAILED,REFUNDED,ADJUSTMENT_REQUIRED")
                .OldAnnotation("Npgsql:Enum:system.survey_pole_count_source", "BASELINE_PUBLICATION,CURRENT_PUBLISHED_INVENTORY")
                .OldAnnotation("Npgsql:Enum:system.survey_refund_obligation_status", "PENDING,RESERVED,EXECUTING,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.survey_refund_origin_kind", "CANCELLATION_RETURN,SERVICE_FAILURE,OVERPAYMENT,COMPLAINT_REFUND")
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
            }

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_payments_purpose",
                schema: "survey",
                table: "survey_payments",
                sql: "purpose IN ('BaselineDeposit', 'FinalBalance')");

            migrationBuilder.DropCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.CreateCheckConstraint(
                name: "ck_drone_missions_source_map",
                schema: "mission",
                table: "drone_missions",
                sql: "(mission_purpose IS NULL AND ((mission_type = 'MAPPING'::system.mission_type AND source_map_version_id IS NULL) OR (mission_type = 'HEALTH_INSPECTION'::system.mission_type AND source_map_version_id IS NOT NULL))) OR (mission_purpose = 'BASELINE_MAPPING'::system.mission_purpose AND source_map_version_id IS NULL AND requires_baseline_completion = FALSE) OR (mission_purpose IN ('PLANT_HEALTH'::system.mission_purpose, 'HARVEST_READINESS'::system.mission_purpose) AND ((requires_baseline_completion = TRUE AND source_map_version_id IS NULL) OR (requires_baseline_completion = FALSE AND source_map_version_id IS NOT NULL)))");
        }
    }
}
