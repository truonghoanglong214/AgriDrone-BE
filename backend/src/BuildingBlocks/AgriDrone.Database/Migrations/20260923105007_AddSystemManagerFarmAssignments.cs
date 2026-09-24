using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriDrone.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemManagerFarmAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
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
                .Annotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .Annotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .Annotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .Annotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
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
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
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
                .OldAnnotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .OldAnnotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .OldAnnotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
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
                name: "system_manager_profiles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "system.system_manager_profile_status", nullable: false),
                    availability = table.Column<int>(type: "system.system_manager_availability_status", nullable: false),
                    qualification_status = table.Column<int>(type: "system.flight_qualification_status", nullable: false),
                    qualification_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_manager_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_manager_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Operational profiles for AgriDrone SystemManagers; independent of customer tenant membership.");

            migrationBuilder.CreateTable(
                name: "farm_manager_assignments",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_manager_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_by = table.Column<Guid>(type: "uuid", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_manager_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_farm_manager_assignments_farms_same_tenant",
                        columns: x => new { x.farm_id, x.tenant_id },
                        principalSchema: "farm",
                        principalTable: "farms",
                        principalColumns: new[] { "id", "tenant_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_manager_assignments_profiles_profile_id",
                        column: x => x.system_manager_profile_id,
                        principalSchema: "identity",
                        principalTable: "system_manager_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_manager_assignments_users_assigned_by",
                        column: x => x.assigned_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_manager_assignments_users_ended_by",
                        column: x => x.ended_by,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Immutable primary SystemManager assignment history for each Farm.");

            migrationBuilder.CreateIndex(
                name: "IX_farm_manager_assignments_assigned_by",
                schema: "identity",
                table: "farm_manager_assignments",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_manager_assignments_ended_by",
                schema: "identity",
                table: "farm_manager_assignments",
                column: "ended_by");

            migrationBuilder.CreateIndex(
                name: "IX_farm_manager_assignments_farm_id_tenant_id",
                schema: "identity",
                table: "farm_manager_assignments",
                columns: new[] { "farm_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_manager_assignments_history",
                schema: "identity",
                table: "farm_manager_assignments",
                columns: new[] { "tenant_id", "farm_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_farm_manager_assignments_profile_active",
                schema: "identity",
                table: "farm_manager_assignments",
                columns: new[] { "system_manager_profile_id", "ended_at" });

            migrationBuilder.CreateIndex(
                name: "uq_farm_manager_assignments_active_farm",
                schema: "identity",
                table: "farm_manager_assignments",
                column: "farm_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_system_manager_profiles_assignability",
                schema: "identity",
                table: "system_manager_profiles",
                columns: new[] { "status", "availability", "qualification_status", "qualification_expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_system_manager_profiles_user",
                schema: "identity",
                table: "system_manager_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "farm_manager_assignments",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "system_manager_profiles",
                schema: "identity");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:system.ai_job_status", "QUEUED,PROCESSING,COMPLETED,FAILED,CANCELLED")
                .Annotation("Npgsql:Enum:system.ai_job_type", "MAPPING,HEALTH_INSPECTION,FRAME_EXTRACTION,PLANT_DETECTION,PLANT_MATCHING,DISEASE_DETECTION")
                .Annotation("Npgsql:Enum:system.ai_model_type", "PLANT_DETECTION,PLANT_TRACKING,PLANT_MATCHING,DISEASE_DETECTION,SEVERITY_ANALYSIS,MULTI_TASK")
                .Annotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .Annotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .Annotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .Annotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .Annotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .Annotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .Annotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .Annotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .Annotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
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
                .Annotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .Annotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .Annotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
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
                .OldAnnotation("Npgsql:Enum:system.altitude_reference", "AGL,MSL,UNKNOWN")
                .OldAnnotation("Npgsql:Enum:system.audit_actor_type", "USER,AI,SYSTEM")
                .OldAnnotation("Npgsql:Enum:system.condition_review_decision", "CONFIRMED,CORRECTED,REJECTED")
                .OldAnnotation("Npgsql:Enum:system.condition_type", "DISEASE,ABIOTIC_DAMAGE,MECHANICAL_DAMAGE,OTHER")
                .OldAnnotation("Npgsql:Enum:system.drone_status", "AVAILABLE,IN_MISSION,MAINTENANCE,INACTIVE,RETIRED")
                .OldAnnotation("Npgsql:Enum:system.farm_access_scope", "ALL_ZONES,SELECTED_ZONES")
                .OldAnnotation("Npgsql:Enum:system.farm_member_role", "MANAGER,WORKER")
                .OldAnnotation("Npgsql:Enum:system.finding_source", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:system.flight_qualification_status", "PENDING,QUALIFIED,SUSPENDED,REVOKED")
                .OldAnnotation("Npgsql:Enum:system.general_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:system.harvest_batch_status", "DRAFT,OPEN,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.harvest_record_source", "WEB,MOBILE,IMPORT")
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
                .OldAnnotation("Npgsql:Enum:system.season_status", "PLANNED,ACTIVE,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.system_manager_availability_status", "AVAILABLE,UNAVAILABLE")
                .OldAnnotation("Npgsql:Enum:system.system_manager_profile_status", "ACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:system.task_priority", "LOW,MEDIUM,HIGH,URGENT")
                .OldAnnotation("Npgsql:Enum:system.task_result", "CONFIRMED_DISEASE,INCORRECT_AI_DETECTION,PLANT_RECOVERED,NEED_FURTHER_INSPECTION,COMPLETED_OTHER")
                .OldAnnotation("Npgsql:Enum:system.task_status", "OPEN,ASSIGNED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:system.task_type", "FIELD_INSPECTION,RECHECK_PLANT,VERIFY_AI_RESULT,GENERAL")
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
