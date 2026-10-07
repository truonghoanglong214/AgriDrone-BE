using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiseaseZonesAndRecommendations : Migration
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
                .Annotation("Npgsql:Enum:system.disease_zone_membership_kind", "PROPOSED,REVIEWED")
                .Annotation("Npgsql:Enum:system.disease_zone_status", "PROPOSED,REVIEWED,PUBLISHED,REJECTED,SUPERSEDED")
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

            migrationBuilder.CreateTable(
                name: "treatment_recommendations",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    plant_condition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    guidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    advisory_disclaimer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expert_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supersedes_recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "system.treatment_recommendation_status", nullable: false, defaultValueSql: "'DRAFT'::system.treatment_recommendation_status"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_by = table.Column<Guid>(type: "uuid", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_by_recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treatment_recommendations", x => x.id);
                    table.CheckConstraint("ck_treatment_recommendations_effective_window", "effective_to IS NULL OR effective_to > effective_from");
                    table.CheckConstraint("ck_treatment_recommendations_lifecycle", "(status = 'DRAFT'::system.treatment_recommendation_status AND published_by IS NULL AND published_at IS NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR (status = 'RETIRED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NOT NULL AND retired_at IS NOT NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NOT NULL AND superseded_at IS NOT NULL)");
                    table.CheckConstraint("ck_treatment_recommendations_supersedes_version", "(version_number = 1 AND supersedes_recommendation_id IS NULL) OR (version_number > 1 AND supersedes_recommendation_id IS NOT NULL)");
                    table.CheckConstraint("ck_treatment_recommendations_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_health_levels",
                        column: x => x.health_level_id,
                        principalSchema: "plant",
                        principalTable: "health_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_plant_conditions",
                        column: x => x.plant_condition_id,
                        principalSchema: "plant",
                        principalTable: "plant_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_superseded_by",
                        column: x => x.superseded_by_recommendation_id,
                        principalSchema: "plant",
                        principalTable: "treatment_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_supersedes",
                        column: x => x.supersedes_recommendation_id,
                        principalSchema: "plant",
                        principalTable: "treatment_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_recommendations_users_retired_by",
                        column: x => x.retired_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disease_zones",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    zone_key = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_boundary_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_base_map_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_condition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    health_level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_proposal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    proposed_geometry = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: false),
                    reviewed_geometry = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: true),
                    recommendation_candidates = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    proposal_evidence = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    current_membership_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<int>(type: "system.disease_zone_status", nullable: false, defaultValueSql: "'PROPOSED'::system.disease_zone_status"),
                    selected_treatment_recommendation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recommendations_rejected = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    review_evidence = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supersedes_disease_zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_by_disease_zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disease_zones", x => x.id);
                    table.UniqueConstraint("uq_disease_zones_id_farm", x => new { x.id, x.farm_id });
                    table.UniqueConstraint("uq_disease_zones_id_tenant_farm", x => new { x.id, x.tenant_id, x.farm_id });
                    table.CheckConstraint("ck_disease_zones_lifecycle", "(status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR (status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)");
                    table.CheckConstraint("ck_disease_zones_membership_version_positive", "current_membership_version >= 1");
                    table.CheckConstraint("ck_disease_zones_proposed_geometry", "NOT ST_IsEmpty(proposed_geometry) AND ST_IsValid(proposed_geometry) AND ST_SRID(proposed_geometry) = 4326 AND GeometryType(proposed_geometry) = 'POLYGON'");
                    table.CheckConstraint("ck_disease_zones_reviewed_geometry", "reviewed_geometry IS NULL OR (NOT ST_IsEmpty(reviewed_geometry) AND ST_IsValid(reviewed_geometry) AND ST_SRID(reviewed_geometry) = 4326 AND GeometryType(reviewed_geometry) = 'POLYGON')");
                    table.CheckConstraint("ck_disease_zones_revision", "(version_number = 1 AND supersedes_disease_zone_id IS NULL) OR (version_number > 1 AND supersedes_disease_zone_id IS NOT NULL)");
                    table.CheckConstraint("ck_disease_zones_version_positive", "version_number >= 1");
                    table.ForeignKey(
                        name: "fk_disease_zones_ai_jobs_source_job_id",
                        column: x => x.source_job_id,
                        principalSchema: "mission",
                        principalTable: "ai_processing_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_base_maps_same_tenant_farm",
                        columns: x => new { x.farm_base_map_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_base_map_versions",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_boundaries_same_tenant_farm",
                        columns: x => new { x.farm_boundary_version_id, x.tenant_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_boundaries",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_health_levels",
                        column: x => x.health_level_id,
                        principalSchema: "plant",
                        principalTable: "health_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_orders_same_tenant_farm",
                        columns: x => new { x.survey_order_id, x.tenant_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "tenant_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_plant_conditions",
                        column: x => x.plant_condition_id,
                        principalSchema: "plant",
                        principalTable: "plant_conditions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_results_same_order_farm",
                        columns: x => new { x.survey_result_id, x.survey_order_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_results",
                        principalColumns: new[] { "id", "survey_order_id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_selected_recommendation",
                        column: x => x.selected_treatment_recommendation_id,
                        principalSchema: "plant",
                        principalTable: "treatment_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_superseded_by_same_farm",
                        columns: x => new { x.superseded_by_disease_zone_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "disease_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_supersedes_same_farm",
                        columns: x => new { x.supersedes_disease_zone_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "disease_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_disease_zones_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disease_zone_memberships",
                schema: "plant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    disease_zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_version = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "system.disease_zone_membership_kind", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disease_zone_memberships", x => x.id);
                    table.CheckConstraint("ck_disease_zone_memberships_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_disease_zone_memberships_version_positive", "membership_version >= 1");
                    table.ForeignKey(
                        name: "fk_disease_zone_memberships_disease_zones",
                        column: x => x.disease_zone_id,
                        principalSchema: "plant",
                        principalTable: "disease_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_disease_zone_memberships_plants_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zone_memberships_plant_id_farm_id",
                schema: "plant",
                table: "disease_zone_memberships",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_disease_zone_memberships_plant_zone",
                schema: "plant",
                table: "disease_zone_memberships",
                columns: new[] { "plant_id", "disease_zone_id" });

            migrationBuilder.CreateIndex(
                name: "uq_disease_zone_memberships_snapshot_plant",
                schema: "plant",
                table: "disease_zone_memberships",
                columns: new[] { "disease_zone_id", "membership_version", "plant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_farm_base_map_version_id_tenant_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "farm_base_map_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_farm_boundary_version_id_tenant_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "farm_boundary_version_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_farm_id_tenant_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_health_level_id",
                schema: "plant",
                table: "disease_zones",
                column: "health_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_plant_condition_id",
                schema: "plant",
                table: "disease_zones",
                column: "plant_condition_id");

            migrationBuilder.CreateIndex(
                name: "ix_disease_zones_proposed_geometry_gist",
                schema: "plant",
                table: "disease_zones",
                column: "proposed_geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_published_by",
                schema: "plant",
                table: "disease_zones",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_disease_zones_review_queue",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "farm_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_reviewed_by",
                schema: "plant",
                table: "disease_zones",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_disease_zones_reviewed_geometry_gist",
                schema: "plant",
                table: "disease_zones",
                column: "reviewed_geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_selected_treatment_recommendation_id",
                schema: "plant",
                table: "disease_zones",
                column: "selected_treatment_recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_source_job_id",
                schema: "plant",
                table: "disease_zones",
                column: "source_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_superseded_by_disease_zone_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "superseded_by_disease_zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_supersedes_disease_zone_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "supersedes_disease_zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_survey_order_id_tenant_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "survey_order_id", "tenant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disease_zones_survey_result_id_survey_order_id_farm_id",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "survey_result_id", "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_disease_zones_key_version",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "zone_key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_disease_zones_one_published",
                schema: "plant",
                table: "disease_zones",
                column: "zone_key",
                unique: true,
                filter: "status = 'PUBLISHED'::system.disease_zone_status");

            migrationBuilder.CreateIndex(
                name: "uq_disease_zones_source_proposal",
                schema: "plant",
                table: "disease_zones",
                columns: new[] { "source_handoff_id", "source_proposal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_treatment_recommendations_applicability",
                schema: "plant",
                table: "treatment_recommendations",
                columns: new[] { "plant_condition_id", "health_level_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_treatment_recommendations_created_by",
                schema: "plant",
                table: "treatment_recommendations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_recommendations_health_level_id",
                schema: "plant",
                table: "treatment_recommendations",
                column: "health_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_recommendations_published_by",
                schema: "plant",
                table: "treatment_recommendations",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_recommendations_retired_by",
                schema: "plant",
                table: "treatment_recommendations",
                column: "retired_by");

            migrationBuilder.CreateIndex(
                name: "IX_treatment_recommendations_superseded_by_recommendation_id",
                schema: "plant",
                table: "treatment_recommendations",
                column: "superseded_by_recommendation_id");

            migrationBuilder.CreateIndex(
                name: "uq_treatment_recommendations_code_version",
                schema: "plant",
                table: "treatment_recommendations",
                columns: new[] { "code", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_treatment_recommendations_one_published",
                schema: "plant",
                table: "treatment_recommendations",
                column: "code",
                unique: true,
                filter: "status = 'PUBLISHED'::system.treatment_recommendation_status");

            migrationBuilder.CreateIndex(
                name: "uq_treatment_recommendations_supersedes",
                schema: "plant",
                table: "treatment_recommendations",
                column: "supersedes_recommendation_id",
                unique: true,
                filter: "supersedes_recommendation_id IS NOT NULL");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION plant.enforce_treatment_recommendation_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'TreatmentRecommendation history cannot be deleted.';
                    END IF;

                    IF NEW.id <> OLD.id
                       OR NEW.code <> OLD.code
                       OR NEW.version_number <> OLD.version_number
                       OR NEW.plant_condition_id <> OLD.plant_condition_id
                       OR NEW.health_level_id <> OLD.health_level_id
                       OR NEW.title <> OLD.title
                       OR NEW.guidance <> OLD.guidance
                       OR NEW.advisory_disclaimer <> OLD.advisory_disclaimer
                       OR NEW.expert_source <> OLD.expert_source
                       OR NEW.source_reference <> OLD.source_reference
                       OR NEW.effective_from <> OLD.effective_from
                       OR NEW.effective_to IS DISTINCT FROM OLD.effective_to
                       OR NEW.supersedes_recommendation_id IS DISTINCT FROM OLD.supersedes_recommendation_id
                       OR NEW.created_by <> OLD.created_by
                       OR NEW.created_at <> OLD.created_at THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'TreatmentRecommendation content and provenance are immutable.';
                    END IF;

                    IF (OLD.status = 'DRAFT'::system.treatment_recommendation_status
                        AND NEW.status NOT IN ('DRAFT'::system.treatment_recommendation_status, 'PUBLISHED'::system.treatment_recommendation_status))
                       OR (OLD.status = 'PUBLISHED'::system.treatment_recommendation_status
                           AND NEW.status NOT IN ('PUBLISHED'::system.treatment_recommendation_status, 'RETIRED'::system.treatment_recommendation_status, 'SUPERSEDED'::system.treatment_recommendation_status))
                       OR (OLD.status IN ('RETIRED'::system.treatment_recommendation_status, 'SUPERSEDED'::system.treatment_recommendation_status)
                           AND NEW.status <> OLD.status) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Invalid TreatmentRecommendation status transition.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_treatment_recommendations_protect_history
                BEFORE UPDATE OR DELETE ON plant.treatment_recommendations
                FOR EACH ROW EXECUTE FUNCTION plant.enforce_treatment_recommendation_history();

                CREATE OR REPLACE FUNCTION plant.enforce_disease_zone_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'DiseaseZone history cannot be deleted.';
                    END IF;

                    IF NEW.id <> OLD.id
                       OR NEW.zone_key <> OLD.zone_key
                       OR NEW.version_number <> OLD.version_number
                       OR NEW.tenant_id <> OLD.tenant_id
                       OR NEW.farm_id <> OLD.farm_id
                       OR NEW.survey_order_id <> OLD.survey_order_id
                       OR NEW.survey_result_id <> OLD.survey_result_id
                       OR NEW.farm_boundary_version_id <> OLD.farm_boundary_version_id
                       OR NEW.farm_base_map_version_id <> OLD.farm_base_map_version_id
                       OR NEW.plant_condition_id <> OLD.plant_condition_id
                       OR NEW.health_level_id <> OLD.health_level_id
                       OR NEW.source_handoff_id <> OLD.source_handoff_id
                       OR NEW.source_job_id IS DISTINCT FROM OLD.source_job_id
                       OR NEW.source_proposal_id <> OLD.source_proposal_id
                       OR NOT ST_Equals(NEW.proposed_geometry, OLD.proposed_geometry)
                       OR NEW.recommendation_candidates IS DISTINCT FROM OLD.recommendation_candidates
                       OR NEW.proposal_evidence IS DISTINCT FROM OLD.proposal_evidence
                       OR NEW.supersedes_disease_zone_id IS DISTINCT FROM OLD.supersedes_disease_zone_id
                       OR NEW.created_at <> OLD.created_at THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'DiseaseZone proposal facts and provenance are immutable.';
                    END IF;

                    IF OLD.status <> 'PROPOSED'::system.disease_zone_status
                       AND (
                           NEW.reviewed_geometry IS DISTINCT FROM OLD.reviewed_geometry
                           OR NEW.current_membership_version <> OLD.current_membership_version
                           OR NEW.selected_treatment_recommendation_id IS DISTINCT FROM OLD.selected_treatment_recommendation_id
                           OR NEW.recommendations_rejected <> OLD.recommendations_rejected
                           OR NEW.reviewed_by IS DISTINCT FROM OLD.reviewed_by
                           OR NEW.reviewed_at IS DISTINCT FROM OLD.reviewed_at
                           OR NEW.review_reason IS DISTINCT FROM OLD.review_reason
                           OR NEW.review_evidence IS DISTINCT FROM OLD.review_evidence
                       ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Reviewed DiseaseZone decision is immutable.';
                    END IF;

                    IF (OLD.status = 'PROPOSED'::system.disease_zone_status
                        AND NEW.status NOT IN ('PROPOSED'::system.disease_zone_status, 'REVIEWED'::system.disease_zone_status, 'REJECTED'::system.disease_zone_status))
                       OR (OLD.status = 'REVIEWED'::system.disease_zone_status
                           AND NEW.status NOT IN ('REVIEWED'::system.disease_zone_status, 'PUBLISHED'::system.disease_zone_status))
                       OR (OLD.status = 'PUBLISHED'::system.disease_zone_status
                           AND NEW.status NOT IN ('PUBLISHED'::system.disease_zone_status, 'SUPERSEDED'::system.disease_zone_status))
                       OR (OLD.status IN ('REJECTED'::system.disease_zone_status, 'SUPERSEDED'::system.disease_zone_status)
                           AND NEW.status <> OLD.status) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Invalid DiseaseZone status transition.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_disease_zones_protect_history
                BEFORE UPDATE OR DELETE ON plant.disease_zones
                FOR EACH ROW EXECUTE FUNCTION plant.enforce_disease_zone_history();

                CREATE OR REPLACE FUNCTION plant.validate_disease_zone_decision()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF NEW.status NOT IN ('REVIEWED'::system.disease_zone_status, 'PUBLISHED'::system.disease_zone_status) THEN
                        RETURN NEW;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM farm.farm_boundaries boundary
                        WHERE boundary.id = NEW.farm_boundary_version_id
                          AND boundary.tenant_id = NEW.tenant_id
                          AND boundary.farm_id = NEW.farm_id
                          AND boundary.status IN ('APPROVED'::system.farm_boundary_status, 'SUPERSEDED'::system.farm_boundary_status)
                          AND ST_Covers(boundary.geometry, NEW.reviewed_geometry)
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Reviewed DiseaseZone must be covered by its approved FarmBoundary version.';
                    END IF;

                    IF NEW.selected_treatment_recommendation_id IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM plant.treatment_recommendations recommendation
                           WHERE recommendation.id = NEW.selected_treatment_recommendation_id
                             AND recommendation.plant_condition_id = NEW.plant_condition_id
                             AND recommendation.health_level_id = NEW.health_level_id
                             AND recommendation.status IN (
                                 'PUBLISHED'::system.treatment_recommendation_status,
                                 'RETIRED'::system.treatment_recommendation_status,
                                 'SUPERSEDED'::system.treatment_recommendation_status)
                             AND recommendation.published_at <= NEW.reviewed_at
                             AND (recommendation.retired_at IS NULL OR NEW.reviewed_at < recommendation.retired_at)
                             AND (recommendation.superseded_at IS NULL OR NEW.reviewed_at < recommendation.superseded_at)
                             AND recommendation.effective_from <= NEW.reviewed_at
                             AND (recommendation.effective_to IS NULL OR NEW.reviewed_at < recommendation.effective_to)
                       ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Selected TreatmentRecommendation is not applicable at review time.';
                    END IF;

                    IF NEW.status = 'PUBLISHED'::system.disease_zone_status THEN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM plant.disease_zone_memberships membership
                            JOIN plant.plants plant
                              ON plant.id = membership.plant_id
                             AND plant.farm_id = membership.farm_id
                            WHERE membership.disease_zone_id = NEW.id
                              AND membership.membership_version = NEW.current_membership_version
                              AND membership.kind = 'REVIEWED'::system.disease_zone_membership_kind
                              AND plant.lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status
                        ) THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Published DiseaseZone requires a reviewed membership snapshot with an active Plant.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM plant.disease_zone_memberships membership
                            JOIN plant.plants plant
                              ON plant.id = membership.plant_id
                             AND plant.farm_id = membership.farm_id
                            WHERE membership.disease_zone_id = NEW.id
                              AND membership.membership_version = NEW.current_membership_version
                              AND membership.kind = 'REVIEWED'::system.disease_zone_membership_kind
                              AND (
                                  plant.lifecycle_status <> 'ACTIVE'::system.plant_lifecycle_status
                                  OR plant.location IS NULL
                                  OR NOT ST_Covers(NEW.reviewed_geometry, plant.location)
                              )
                        ) THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Every published DiseaseZone member must be an active Plant covered by reviewed geometry.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_disease_zones_validate_decision
                BEFORE INSERT OR UPDATE OF status, reviewed_geometry, selected_treatment_recommendation_id, reviewed_at ON plant.disease_zones
                FOR EACH ROW EXECUTE FUNCTION plant.validate_disease_zone_decision();

                CREATE OR REPLACE FUNCTION plant.enforce_disease_zone_membership_history()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'DiseaseZone membership history cannot be deleted.';
                    END IF;

                    IF NEW IS DISTINCT FROM OLD THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'DiseaseZone membership snapshots are immutable.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER tr_disease_zone_memberships_protect_history
                BEFORE UPDATE OR DELETE ON plant.disease_zone_memberships
                FOR EACH ROW EXECUTE FUNCTION plant.enforce_disease_zone_membership_history();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM plant.disease_zones)
                       OR EXISTS (SELECT 1 FROM plant.treatment_recommendations) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = 'P0001',
                            MESSAGE = '0R-4 rollback blocked: DiseaseZone or TreatmentRecommendation data exists.';
                    END IF;
                END $$;

                DROP TRIGGER IF EXISTS tr_disease_zone_memberships_protect_history ON plant.disease_zone_memberships;
                DROP FUNCTION IF EXISTS plant.enforce_disease_zone_membership_history();
                DROP TRIGGER IF EXISTS tr_disease_zones_validate_decision ON plant.disease_zones;
                DROP FUNCTION IF EXISTS plant.validate_disease_zone_decision();
                DROP TRIGGER IF EXISTS tr_disease_zones_protect_history ON plant.disease_zones;
                DROP FUNCTION IF EXISTS plant.enforce_disease_zone_history();
                DROP TRIGGER IF EXISTS tr_treatment_recommendations_protect_history ON plant.treatment_recommendations;
                DROP FUNCTION IF EXISTS plant.enforce_treatment_recommendation_history();
                """);

            migrationBuilder.DropTable(
                name: "disease_zone_memberships",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "disease_zones",
                schema: "plant");

            migrationBuilder.DropTable(
                name: "treatment_recommendations",
                schema: "plant");

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
                .OldAnnotation("Npgsql:Enum:system.treatment_recommendation_status", "DRAFT,PUBLISHED,RETIRED,SUPERSEDED")
                .OldAnnotation("Npgsql:Enum:system.user_status", "ACTIVE,INACTIVE,LOCKED")
                .OldAnnotation("Npgsql:Enum:system.verification_decision", "CONFIRMED,CORRECTED,REJECTED,FIELD_INSPECTION_REQUIRED,INCORRECT,NEED_FIELD_INSPECTION,RECOVERED")
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
