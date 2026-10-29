using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class FarmFirstMappingV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mapping_candidates_candidate_zone",
                schema: "farm",
                table: "mapping_candidates");

            migrationBuilder.DropTable(
                name: "mapping_candidate_zones",
                schema: "farm");

            migrationBuilder.DropIndex(
                name: "ux_plants_active_zone_grid_position",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "IX_mapping_candidates_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidates");

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

            migrationBuilder.AddColumn<int>(
                name: "scope_type",
                schema: "survey",
                table: "survey_orders",
                type: "system.survey_scope_type",
                nullable: false,
                defaultValueSql: "'FARM_WIDE'::system.survey_scope_type");

            migrationBuilder.AddColumn<Guid>(
                name: "farm_base_map_version_id",
                schema: "plant",
                table: "plants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "zone_id",
                schema: "farm",
                table: "mapping_candidates",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<double>(
                name: "grid_bearing_deg",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "plant_spacing_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "row_spacing_m",
                schema: "farm",
                table: "mapping_candidate_batches",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "grid_bearing_deg",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "plant_spacing_m",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "row_spacing_m",
                schema: "farm",
                table: "farm_base_map_versions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "scope_fingerprint",
                schema: "mission",
                table: "drone_missions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "FARM_WIDE");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "scope_snapshot",
                schema: "mission",
                table: "drone_missions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "scope_type",
                schema: "mission",
                table: "drone_missions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FARM_WIDE");

            migrationBuilder.CreateTable(
                name: "farm_zone_plant_memberships",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_assigned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_zone_plant_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_farm_zone_plant_memberships_plant_same_farm",
                        columns: x => new { x.plant_id, x.farm_id },
                        principalSchema: "plant",
                        principalTable: "plants",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_zone_plant_memberships_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_zone_plant_memberships_zone_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "survey_order_zone_scopes",
                schema: "survey",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    survey_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_version = table.Column<long>(type: "bigint", nullable: false),
                    boundary_snapshot = table.Column<Polygon>(type: "geometry(Polygon,4326)", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_survey_order_zone_scopes", x => x.id);
                    table.ForeignKey(
                        name: "fk_survey_order_zone_scopes_order_farm",
                        columns: x => new { x.survey_order_id, x.farm_id },
                        principalSchema: "survey",
                        principalTable: "survey_orders",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_survey_order_zone_scopes_zone_same_farm",
                        columns: x => new { x.zone_id, x.farm_id },
                        principalSchema: "farm",
                        principalTable: "farm_zones",
                        principalColumns: new[] { "id", "farm_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE plant.plants AS plant_row
                SET farm_base_map_version_id = zone_map.farm_base_map_version_id
                FROM farm.zone_map_versions AS zone_map
                WHERE plant_row.current_map_version_id = zone_map.id
                  AND plant_row.farm_id = zone_map.farm_id
                  AND plant_row.farm_base_map_version_id IS NULL;

                INSERT INTO farm.farm_zone_plant_memberships (
                    id, farm_id, zone_id, plant_id, source, is_assigned,
                    assigned_by, reason, effective_from, effective_to)
                SELECT gen_random_uuid(), plant_row.farm_id, plant_row.zone_id,
                       plant_row.id, 'AUTO_GEOMETRY', TRUE, zone.created_by,
                       'Backfilled from legacy Plant.ZoneId',
                       GREATEST(plant_row.created_at, zone.created_at), NULL
                FROM plant.plants AS plant_row
                JOIN farm.farm_zones AS zone
                  ON zone.id = plant_row.zone_id
                 AND zone.farm_id = plant_row.farm_id
                WHERE plant_row.zone_id IS NOT NULL;

                UPDATE mission.drone_missions
                SET scope_type = 'ZONES',
                    scope_fingerprint = 'LEGACY_SCOPE_REQUIRES_REPREPARE'
                WHERE cardinality(scope_zone_ids) > 0;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_plants_farm_base_map_version",
                schema: "plant",
                table: "plants",
                column: "farm_base_map_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_plants_farm_base_map_version_id_farm_id",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_base_map_version_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ux_plants_active_farm_map_grid_position",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_base_map_version_id", "row_index", "column_index" },
                unique: true,
                filter: "farm_base_map_version_id IS NOT NULL AND row_index IS NOT NULL AND column_index IS NOT NULL AND lifecycle_status IN ('ACTIVE'::system.plant_lifecycle_status, 'MISSING'::system.plant_lifecycle_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants",
                sql: "((current_map_version_id IS NULL AND farm_base_map_version_id IS NULL) AND row_index IS NULL AND column_index IS NULL) OR (((current_map_version_id IS NOT NULL AND zone_id IS NOT NULL) OR farm_base_map_version_id IS NOT NULL) AND row_index IS NOT NULL AND column_index IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_farm_zone_plant_memberships_assigned_by",
                schema: "farm",
                table: "farm_zone_plant_memberships",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_zone_plant_memberships_plant_id_farm_id",
                schema: "farm",
                table: "farm_zone_plant_memberships",
                columns: new[] { "plant_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_farm_zone_plant_memberships_zone_id_farm_id",
                schema: "farm",
                table: "farm_zone_plant_memberships",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_zone_plant_memberships_zone_plant",
                schema: "farm",
                table: "farm_zone_plant_memberships",
                columns: new[] { "zone_id", "plant_id" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_zone_plant_memberships_one_active_per_plant",
                schema: "farm",
                table: "farm_zone_plant_memberships",
                column: "plant_id",
                unique: true,
                filter: "effective_to IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_survey_order_zone_scopes_farm_zone",
                schema: "survey",
                table: "survey_order_zone_scopes",
                columns: new[] { "farm_id", "zone_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_zone_scopes_survey_order_id_farm_id",
                schema: "survey",
                table: "survey_order_zone_scopes",
                columns: new[] { "survey_order_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "IX_survey_order_zone_scopes_zone_id_farm_id",
                schema: "survey",
                table: "survey_order_zone_scopes",
                columns: new[] { "zone_id", "farm_id" });

            migrationBuilder.CreateIndex(
                name: "uq_survey_order_zone_scopes_order_zone",
                schema: "survey",
                table: "survey_order_zone_scopes",
                columns: new[] { "survey_order_id", "zone_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_plants_farm_base_map_version_same_farm",
                schema: "plant",
                table: "plants",
                columns: new[] { "farm_base_map_version_id", "farm_id" },
                principalSchema: "farm",
                principalTable: "farm_base_map_versions",
                principalColumns: new[] { "id", "farm_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_plants_farm_base_map_version_same_farm",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropTable(
                name: "farm_zone_plant_memberships",
                schema: "farm");

            migrationBuilder.DropTable(
                name: "survey_order_zone_scopes",
                schema: "survey");

            migrationBuilder.DropIndex(
                name: "ix_plants_farm_base_map_version",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "IX_plants_farm_base_map_version_id_farm_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropIndex(
                name: "ux_plants_active_farm_map_grid_position",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "scope_type",
                schema: "survey",
                table: "survey_orders");

            migrationBuilder.DropColumn(
                name: "farm_base_map_version_id",
                schema: "plant",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "grid_bearing_deg",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "plant_spacing_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "row_spacing_m",
                schema: "farm",
                table: "mapping_candidate_batches");

            migrationBuilder.DropColumn(
                name: "grid_bearing_deg",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "plant_spacing_m",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "row_spacing_m",
                schema: "farm",
                table: "farm_base_map_versions");

            migrationBuilder.DropColumn(
                name: "scope_fingerprint",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "scope_snapshot",
                schema: "mission",
                table: "drone_missions");

            migrationBuilder.DropColumn(
                name: "scope_type",
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

            migrationBuilder.AlterColumn<Guid>(
                name: "zone_id",
                schema: "farm",
                table: "mapping_candidates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "mapping_candidate_zones",
                schema: "farm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expected_current_zone_map_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grid_bearing_deg = table.Column<double>(type: "double precision", nullable: false),
                    plant_spacing_m = table.Column<double>(type: "double precision", nullable: false),
                    row_spacing_m = table.Column<double>(type: "double precision", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_candidate_zones", x => x.id);
                    table.UniqueConstraint("AK_mapping_candidate_zones_batch_id_zone_id", x => new { x.batch_id, x.zone_id });
                    table.ForeignKey(
                        name: "fk_mapping_candidate_zones_batch",
                        column: x => x.batch_id,
                        principalSchema: "farm",
                        principalTable: "mapping_candidate_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_plants_active_zone_grid_position",
                schema: "plant",
                table: "plants",
                columns: new[] { "zone_id", "row_index", "column_index" },
                unique: true,
                filter: "row_index IS NOT NULL AND column_index IS NOT NULL AND lifecycle_status IN ('ACTIVE'::system.plant_lifecycle_status, 'MISSING'::system.plant_lifecycle_status)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_plants_grid_position_complete",
                schema: "plant",
                table: "plants",
                sql: "(current_map_version_id IS NULL AND row_index IS NULL AND column_index IS NULL) OR (current_map_version_id IS NOT NULL AND zone_id IS NOT NULL AND row_index IS NOT NULL AND column_index IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_mapping_candidates_batch_id_zone_id",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "zone_id" });

            migrationBuilder.CreateIndex(
                name: "uq_mapping_candidate_zones_batch_zone",
                schema: "farm",
                table: "mapping_candidate_zones",
                columns: new[] { "batch_id", "zone_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_mapping_candidates_candidate_zone",
                schema: "farm",
                table: "mapping_candidates",
                columns: new[] { "batch_id", "zone_id" },
                principalSchema: "farm",
                principalTable: "mapping_candidate_zones",
                principalColumns: new[] { "batch_id", "zone_id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
