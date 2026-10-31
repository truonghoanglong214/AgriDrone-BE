using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropSurveyAuthorizationsAndManagerQualifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_capture_authorizations",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "ix_system_manager_profiles_assignability",
                schema: "identity",
                table: "system_manager_profiles");

            migrationBuilder.DropColumn(
                name: "qualification_expires_at",
                schema: "identity",
                table: "system_manager_profiles");

            migrationBuilder.DropColumn(
                name: "qualification_status",
                schema: "identity",
                table: "system_manager_profiles");

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
                .OldAnnotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .OldAnnotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_base_map_status", "DRAFT,PUBLISHED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_source", "APPLICANT,TENANT_OWNER,SYSTEM_MANAGER,LEGACY_IMPORT")
                .OldAnnotation("Npgsql:Enum:system.farm_boundary_status", "DRAFT,APPROVED,REJECTED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
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
                .OldAnnotation("Npgsql:Enum:system.survey_capture_authorization_status", "PENDING,VERIFIED,REVOKED")
                .OldAnnotation("Npgsql:Enum:system.survey_capture_authorization_type", "WRITTEN_DATA_USE_CONSENT,FLIGHT_PERMISSION")
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

            migrationBuilder.CreateIndex(
                name: "ix_system_manager_profiles_assignability",
                schema: "identity",
                table: "system_manager_profiles",
                columns: new[] { "status", "availability" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_system_manager_profiles_assignability",
                schema: "identity",
                table: "system_manager_profiles");

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
                .Annotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
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
                .Annotation("Npgsql:Enum:system.survey_capture_authorization_status", "PENDING,VERIFIED,REVOKED")
                .Annotation("Npgsql:Enum:system.survey_capture_authorization_type", "WRITTEN_DATA_USE_CONSENT,FLIGHT_PERMISSION")
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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "qualification_expires_at",
                schema: "identity",
                table: "system_manager_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "qualification_status",
                schema: "identity",
                table: "system_manager_profiles",
                type: "system.flight_qualification_status",
                nullable: false,
                defaultValueSql: "'PENDING'::system.flight_qualification_status");

            migrationBuilder.CreateTable(
                name: "survey_capture_authorizations",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "system.survey_capture_authorization_status", nullable: false),
                    authorization_type = table.Column<int>(type: "system.survey_capture_authorization_type", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_capture_authorizations", x => x.id);
                    table.CheckConstraint("ck_capture_authorizations_expiry", "expires_at IS NULL OR expires_at > verified_at");
                    table.CheckConstraint("ck_capture_authorizations_revocation", "(status <> 'REVOKED'::system.survey_capture_authorization_status) OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revocation_reason IS NOT NULL)");
                    table.CheckConstraint("ck_capture_authorizations_verified_evidence", "(status <> 'VERIFIED'::system.survey_capture_authorization_status) OR (evidence_reference IS NOT NULL AND issued_at IS NOT NULL AND verified_at IS NOT NULL AND verified_by IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_capture_authorizations_orders_order_id",
                        column: x => x.survey_order_id,
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_manager_profiles_assignability",
                schema: "identity",
                table: "system_manager_profiles",
                columns: new[] { "status", "availability", "qualification_status", "qualification_expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_capture_authorizations_order_type",
                schema: "survey",
                table: "survey_capture_authorizations",
                columns: new[] { "survey_order_id", "authorization_type" },
                unique: true);
        }
    }
}
