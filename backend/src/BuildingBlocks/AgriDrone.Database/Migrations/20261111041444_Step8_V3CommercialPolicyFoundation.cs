using System;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class Step8_V3CommercialPolicyFoundation : Migration
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
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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

            migrationBuilder.AlterColumn<SurveyServiceType>(
                name: "service_type",
                schema: "survey",
                table: "survey_services",
                type: "system.survey_service_type",
                nullable: true,
                oldClrType: typeof(SurveyServiceType),
                oldType: "system.survey_service_type");

            migrationBuilder.AddColumn<ContractScope>(
                name: "scope",
                schema: "survey",
                table: "survey_services",
                type: "system.contract_scope",
                nullable: false,
                defaultValue: ContractScope.Legacy);

            migrationBuilder.AddColumn<string>(
                name: "evidence_reference",
                schema: "survey",
                table: "survey_service_prices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "predecessor_version_id",
                schema: "survey",
                table: "survey_service_prices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                schema: "survey",
                table: "survey_service_prices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                schema: "survey",
                table: "survey_service_prices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cancellation_policies",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refundable_before_capture = table.Column<bool>(type: "boolean", nullable: false),
                    retain_deposit_after_capture = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_free_no_show_reschedules = table.Column<int>(type: "integer", nullable: false),
                    operational_postponement_refundable = table.Column<bool>(type: "boolean", nullable: false),
                    service_failure_refundable = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cancellation_policies", x => x.id);
                    table.CheckConstraint("ck_cancellation_policies_no_shows", "allowed_free_no_show_reschedules >= 0");
                    table.CheckConstraint("ck_cancellation_policies_version", "version_number >= 1");
                    table.CheckConstraint("ck_cancellation_policies_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_cancellation_policies_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "cancellation_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_policies",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    calendar_window_days = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    exclude_notice_date = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_policies", x => x.id);
                    table.CheckConstraint("ck_claim_policies_days", "calendar_window_days >= 1");
                    table.CheckConstraint("ck_claim_policies_version", "version_number >= 1");
                    table.CheckConstraint("ck_claim_policies_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_claim_policies_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "claim_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_deposit_policies",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_deposit_policies", x => x.id);
                    table.CheckConstraint("ck_survey_deposit_policies_amount", "amount > 0 AND amount = FLOOR(amount)");
                    table.CheckConstraint("ck_survey_deposit_policies_currency", "currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_survey_deposit_policies_version", "version_number >= 1");
                    table.CheckConstraint("ck_survey_deposit_policies_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_survey_deposit_policies_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "survey_deposit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_service_revisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    maps_poles = table.Column<bool>(type: "boolean", nullable: false),
                    screens_disease = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_service_revisions", x => x.id);
                    table.CheckConstraint("ck_survey_service_revisions_capabilities", "maps_poles OR screens_disease");
                    table.CheckConstraint("ck_survey_service_revisions_version", "version_number >= 1");
                    table.CheckConstraint("ck_survey_service_revisions_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_survey_service_revisions_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "survey_service_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_survey_service_revisions_services_service_id",
                        column: x => x.survey_service_id,
                        principalSchema: "survey",
                        principalTable: "survey_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_policy_versions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    predecessor_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    survey_deposit_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_revision_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false, defaultValueSql: "ARRAY[]::uuid[]"),
                    pricing_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_policy_versions", x => x.id);
                    table.CheckConstraint("ck_business_policy_versions_currency", "pricing_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_business_policy_versions_version", "version_number >= 1");
                    table.CheckConstraint("ck_business_policy_versions_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "fk_business_policy_versions_cancellation_policy",
                        column: x => x.cancellation_policy_id,
                        principalSchema: "survey",
                        principalTable: "cancellation_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_business_policy_versions_claim_policy",
                        column: x => x.claim_policy_id,
                        principalSchema: "survey",
                        principalTable: "claim_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_business_policy_versions_deposit_policy",
                        column: x => x.survey_deposit_policy_id,
                        principalSchema: "survey",
                        principalTable: "survey_deposit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_business_policy_versions_predecessor",
                        column: x => x.predecessor_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_policy_activation",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_intake_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    capture_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    release_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_policy_activation", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_policy_activation_policy_version",
                        column: x => x.active_business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_policy_activation_decisions",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_intake_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    capture_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    release_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    predecessor_decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_policy_activation_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_policy_activation_decisions_activation",
                        column: x => x.activation_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_activation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_business_policy_activation_decisions_policy_version",
                        column: x => x.business_policy_version_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_business_policy_activation_decisions_predecessor",
                        column: x => x.predecessor_decision_id,
                        principalSchema: "survey",
                        principalTable: "business_policy_activation_decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "scope",
                value: ContractScope.Legacy);

            migrationBuilder.UpdateData(
                schema: "survey",
                table: "survey_services",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "scope",
                value: ContractScope.Legacy);

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_services_scope_type",
                schema: "survey",
                table: "survey_services",
                sql: "(scope = 'LEGACY' AND service_type IS NOT NULL) OR (scope = 'V3' AND service_type IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_survey_service_prices_predecessor_version_id",
                schema: "survey",
                table: "survey_service_prices",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_service_prices_service_version",
                schema: "survey",
                table: "survey_service_prices",
                columns: new[] { "survey_service_id", "version_number" },
                unique: true,
                filter: "version_number IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_survey_service_prices_version",
                schema: "survey",
                table: "survey_service_prices",
                sql: "version_number IS NULL OR version_number >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_activation_active_business_policy_version_id",
                schema: "survey",
                table: "business_policy_activation",
                column: "active_business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_activation_decisions_activation_id",
                schema: "survey",
                table: "business_policy_activation_decisions",
                column: "activation_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_activation_decisions_business_policy_versio~",
                schema: "survey",
                table: "business_policy_activation_decisions",
                column: "business_policy_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_policy_activation_decisions_changed_at",
                schema: "survey",
                table: "business_policy_activation_decisions",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_activation_decisions_predecessor_decision_id",
                schema: "survey",
                table: "business_policy_activation_decisions",
                column: "predecessor_decision_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_versions_cancellation_policy_id",
                schema: "survey",
                table: "business_policy_versions",
                column: "cancellation_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_versions_claim_policy_id",
                schema: "survey",
                table: "business_policy_versions",
                column: "claim_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_policy_versions_effective",
                schema: "survey",
                table: "business_policy_versions",
                columns: new[] { "stream_key", "effective_from" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_versions_predecessor_version_id",
                schema: "survey",
                table: "business_policy_versions",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_policy_versions_survey_deposit_policy_id",
                schema: "survey",
                table: "business_policy_versions",
                column: "survey_deposit_policy_id");

            migrationBuilder.CreateIndex(
                name: "uq_business_policy_versions_stream_version",
                schema: "survey",
                table: "business_policy_versions",
                columns: new[] { "stream_key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cancellation_policies_effective",
                schema: "survey",
                table: "cancellation_policies",
                column: "effective_from",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_cancellation_policies_predecessor_version_id",
                schema: "survey",
                table: "cancellation_policies",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_cancellation_policies_version",
                schema: "survey",
                table: "cancellation_policies",
                column: "version_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_claim_policies_effective",
                schema: "survey",
                table: "claim_policies",
                column: "effective_from",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_claim_policies_predecessor_version_id",
                schema: "survey",
                table: "claim_policies",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_claim_policies_version",
                schema: "survey",
                table: "claim_policies",
                column: "version_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_deposit_policies_effective",
                schema: "survey",
                table: "survey_deposit_policies",
                column: "effective_from",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_survey_deposit_policies_predecessor_version_id",
                schema: "survey",
                table: "survey_deposit_policies",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_deposit_policies_version",
                schema: "survey",
                table: "survey_deposit_policies",
                column: "version_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_survey_service_revisions_effective",
                schema: "survey",
                table: "survey_service_revisions",
                columns: new[] { "survey_service_id", "effective_from" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_survey_service_revisions_predecessor_version_id",
                schema: "survey",
                table: "survey_service_revisions",
                column: "predecessor_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_survey_service_revisions_service_version",
                schema: "survey",
                table: "survey_service_revisions",
                columns: new[] { "survey_service_id", "version_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_survey_service_prices_predecessor",
                schema: "survey",
                table: "survey_service_prices",
                column: "predecessor_version_id",
                principalSchema: "survey",
                principalTable: "survey_service_prices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_survey_service_prices_predecessor",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropTable(
                name: "business_policy_activation_decisions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_service_revisions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "business_policy_activation",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "business_policy_versions",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "cancellation_policies",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "claim_policies",
                schema: "survey");

            migrationBuilder.DropTable(
                name: "survey_deposit_policies",
                schema: "survey");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_services_scope_type",
                schema: "survey",
                table: "survey_services");

            migrationBuilder.DropIndex(
                name: "IX_survey_service_prices_predecessor_version_id",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropIndex(
                name: "uq_survey_service_prices_service_version",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_survey_service_prices_version",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "survey",
                table: "survey_services");

            migrationBuilder.DropColumn(
                name: "evidence_reference",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "predecessor_version_id",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "reason_code",
                schema: "survey",
                table: "survey_service_prices");

            migrationBuilder.DropColumn(
                name: "version_number",
                schema: "survey",
                table: "survey_service_prices");

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
                .Annotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .Annotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .Annotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_purpose", "BASELINE_MAPPING,PAID_SERVICE")
                .OldAnnotation("Npgsql:Enum:system.survey_appointment_status", "PROPOSED,CONFIRMED,RESCHEDULE_REQUESTED,CANCELLED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.survey_order_status", "PENDING_BOUNDARY_VERIFICATION,AWAITING_BASELINE_APPOINTMENT,AWAITING_BASELINE_DEPOSIT,AWAITING_BOUNDARY_CAPTURE,BASELINE_READY,BASELINE_IN_PROGRESS,AWAITING_BASELINE_REVIEW,AWAITING_PRICING,AWAITING_PAID_APPOINTMENT,AWAITING_PAYMENT,READY_FOR_PAID_SERVICE,IN_PROGRESS,PENDING_REVIEW,COMPLETED,CANCELLED")
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

            migrationBuilder.AlterColumn<SurveyServiceType>(
                name: "service_type",
                schema: "survey",
                table: "survey_services",
                type: "system.survey_service_type",
                nullable: false,
                defaultValue: SurveyServiceType.PlantHealth,
                oldClrType: typeof(SurveyServiceType),
                oldType: "system.survey_service_type",
                oldNullable: true);
        }
    }
}
