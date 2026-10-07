using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantInventoryChangeReportConfiguration
    : IEntityTypeConfiguration<PlantInventoryChangeReport>
{
    private const string OpenStatuses =
        "'SUBMITTED'::system.plant_inventory_change_status, " +
        "'UNDER_REVIEW'::system.plant_inventory_change_status, " +
        "'AWAITING_SURVEY_EVIDENCE'::system.plant_inventory_change_status, " +
        "'VERIFIED'::system.plant_inventory_change_status";

    public void Configure(EntityTypeBuilder<PlantInventoryChangeReport> builder)
    {
        builder.ToTable(
            "plant_inventory_change_reports",
            "plant",
            table =>
            {
                table.HasComment(
                    "Owner-reported plant removal, replacement and new-plant claims; official inventory changes only in a verified mapping/amendment transaction.");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_context",
                    "(kind IN ('REMOVED'::system.plant_inventory_change_kind, 'REPLACED'::system.plant_inventory_change_kind) AND existing_plant_id IS NOT NULL) OR " +
                    "(kind = 'NEW_PLANT'::system.plant_inventory_change_kind AND existing_plant_id IS NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_location",
                    "NOT ST_IsEmpty(reported_location) AND ST_IsValid(reported_location) AND " +
                    "ST_SRID(reported_location) = 4326 AND GeometryType(reported_location) = 'POINT'");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_review_started",
                    "(review_started_by IS NULL AND review_started_at IS NULL) OR " +
                    "(review_started_by IS NOT NULL AND review_started_at IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_evidence_request",
                    "(evidence_requested_by IS NULL AND evidence_requested_at IS NULL AND evidence_request_reason IS NULL AND evidence_request_details IS NULL) OR " +
                    "(evidence_requested_by IS NOT NULL AND evidence_requested_at IS NOT NULL AND evidence_request_reason IS NOT NULL AND evidence_request_details IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_survey_evidence",
                    "(evidence_survey_order_id IS NULL AND evidence_mission_id IS NULL AND evidence_candidate_reference IS NULL AND evidence_farm_boundary_version_id IS NULL AND evidence_farm_base_map_version_id IS NULL AND survey_evidence IS NULL) OR " +
                    "(kind = 'NEW_PLANT'::system.plant_inventory_change_kind AND evidence_survey_order_id IS NOT NULL AND evidence_mission_id IS NOT NULL AND evidence_candidate_reference IS NOT NULL AND evidence_farm_boundary_version_id IS NOT NULL AND survey_evidence IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_verification",
                    "(verified_by IS NULL AND verified_at IS NULL AND verification_reason IS NULL AND verification_evidence IS NULL) OR " +
                    "(verified_by IS NOT NULL AND verified_at IS NOT NULL AND verification_reason IS NOT NULL AND verification_evidence IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_rejection",
                    "(rejected_by IS NULL AND rejected_at IS NULL AND rejection_reason IS NULL AND rejection_evidence IS NULL) OR " +
                    "(rejected_by IS NOT NULL AND rejected_at IS NOT NULL AND rejection_reason IS NOT NULL AND rejection_evidence IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_withdrawal",
                    "(withdrawn_by IS NULL AND withdrawn_at IS NULL) OR " +
                    "(withdrawn_by IS NOT NULL AND withdrawn_at IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_application",
                    "(applied_farm_boundary_version_id IS NULL AND applied_farm_base_map_version_id IS NULL AND applied_by IS NULL AND applied_at IS NULL AND application_reason IS NULL AND application_evidence IS NULL AND resulting_plant_id IS NULL) OR " +
                    "(applied_farm_boundary_version_id IS NOT NULL AND applied_farm_base_map_version_id IS NOT NULL AND applied_by IS NOT NULL AND applied_at IS NOT NULL AND application_reason IS NOT NULL AND application_evidence IS NOT NULL AND " +
                    "((kind = 'REMOVED'::system.plant_inventory_change_kind AND resulting_plant_id IS NULL) OR " +
                    "(kind IN ('REPLACED'::system.plant_inventory_change_kind, 'NEW_PLANT'::system.plant_inventory_change_kind) AND resulting_plant_id IS NOT NULL)))");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_status",
                    "(status = 'SUBMITTED'::system.plant_inventory_change_status AND review_started_by IS NULL AND verified_by IS NULL AND rejected_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NULL) OR " +
                    "(status = 'UNDER_REVIEW'::system.plant_inventory_change_status AND review_started_by IS NOT NULL AND verified_by IS NULL AND rejected_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NULL) OR " +
                    "(status = 'AWAITING_SURVEY_EVIDENCE'::system.plant_inventory_change_status AND kind = 'NEW_PLANT'::system.plant_inventory_change_kind AND evidence_requested_by IS NOT NULL AND verified_by IS NULL AND rejected_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NULL) OR " +
                    "(status = 'VERIFIED'::system.plant_inventory_change_status AND verified_by IS NOT NULL AND rejected_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NULL AND ((kind = 'NEW_PLANT'::system.plant_inventory_change_kind AND survey_evidence IS NOT NULL) OR (kind <> 'NEW_PLANT'::system.plant_inventory_change_kind AND survey_evidence IS NULL))) OR " +
                    "(status = 'APPLIED'::system.plant_inventory_change_status AND verified_by IS NOT NULL AND rejected_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NOT NULL) OR " +
                    "(status = 'REJECTED'::system.plant_inventory_change_status AND rejected_by IS NOT NULL AND verified_by IS NULL AND withdrawn_by IS NULL AND applied_by IS NULL) OR " +
                    "(status = 'WITHDRAWN'::system.plant_inventory_change_status AND withdrawn_by IS NOT NULL AND verified_by IS NULL AND rejected_by IS NULL AND applied_by IS NULL)");
                table.HasCheckConstraint(
                    "ck_plant_inventory_change_reports_replacement_identity",
                    "kind <> 'REPLACED'::system.plant_inventory_change_kind OR resulting_plant_id IS NULL OR resulting_plant_id <> existing_plant_id");
            });

        builder.HasKey(report => report.Id)
            .HasName("pk_plant_inventory_change_reports");
        builder.HasAlternateKey(report => new { report.Id, report.TenantId, report.FarmId })
            .HasName("uq_plant_inventory_change_reports_id_tenant_farm");

        builder.Property(report => report.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(report => report.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(report => report.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(report => report.ReportedByUserId).HasColumnName("reported_by_user_id").HasColumnType("uuid");
        builder.Property(report => report.Kind).HasColumnName("kind").HasColumnType("system.plant_inventory_change_kind").IsRequired();
        builder.Property(report => report.Status).HasColumnName("status").HasColumnType("system.plant_inventory_change_status").HasSentinel((PlantInventoryChangeStatus)(-1)).HasDefaultValueSql("'SUBMITTED'::system.plant_inventory_change_status").IsRequired();
        builder.Property(report => report.ExistingPlantId).HasColumnName("existing_plant_id").HasColumnType("uuid");
        builder.Property(report => report.ReportedLocation).HasColumnName("reported_location").HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(report => report.PoleLocationKey).HasColumnName("pole_location_key").HasColumnType("character varying(80)").HasMaxLength(80).IsRequired();
        builder.Property(report => report.CallerScope).HasColumnName("caller_scope").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(report => report.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(report => report.ReportReason).HasColumnName("report_reason").HasColumnType("character varying(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(report => report.ReportEvidence).HasColumnName("report_evidence").HasColumnType("jsonb").IsRequired();
        builder.Property(report => report.ReviewStartedBy).HasColumnName("review_started_by").HasColumnType("uuid");
        builder.Property(report => report.ReviewStartedAt).HasColumnName("review_started_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.EvidenceRequestedBy).HasColumnName("evidence_requested_by").HasColumnType("uuid");
        builder.Property(report => report.EvidenceRequestedAt).HasColumnName("evidence_requested_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.EvidenceRequestReason).HasColumnName("evidence_request_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(report => report.EvidenceRequestDetails).HasColumnName("evidence_request_details").HasColumnType("jsonb");
        builder.Property(report => report.EvidenceSurveyOrderId).HasColumnName("evidence_survey_order_id").HasColumnType("uuid");
        builder.Property(report => report.EvidenceMissionId).HasColumnName("evidence_mission_id").HasColumnType("uuid");
        builder.Property(report => report.EvidenceCandidateReference).HasColumnName("evidence_candidate_reference").HasColumnType("character varying(200)").HasMaxLength(200);
        builder.Property(report => report.EvidenceFarmBoundaryVersionId).HasColumnName("evidence_farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(report => report.EvidenceFarmBaseMapVersionId).HasColumnName("evidence_farm_base_map_version_id").HasColumnType("uuid");
        builder.Property(report => report.SurveyEvidence).HasColumnName("survey_evidence").HasColumnType("jsonb");
        builder.Property(report => report.VerifiedBy).HasColumnName("verified_by").HasColumnType("uuid");
        builder.Property(report => report.VerifiedAt).HasColumnName("verified_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.VerificationReason).HasColumnName("verification_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(report => report.VerificationEvidence).HasColumnName("verification_evidence").HasColumnType("jsonb");
        builder.Property(report => report.RejectedBy).HasColumnName("rejected_by").HasColumnType("uuid");
        builder.Property(report => report.RejectedAt).HasColumnName("rejected_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.RejectionReason).HasColumnName("rejection_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(report => report.RejectionEvidence).HasColumnName("rejection_evidence").HasColumnType("jsonb");
        builder.Property(report => report.WithdrawnBy).HasColumnName("withdrawn_by").HasColumnType("uuid");
        builder.Property(report => report.WithdrawnAt).HasColumnName("withdrawn_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.ResultingPlantId).HasColumnName("resulting_plant_id").HasColumnType("uuid");
        builder.Property(report => report.AppliedFarmBoundaryVersionId).HasColumnName("applied_farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(report => report.AppliedFarmBaseMapVersionId).HasColumnName("applied_farm_base_map_version_id").HasColumnType("uuid");
        builder.Property(report => report.AppliedBy).HasColumnName("applied_by").HasColumnType("uuid");
        builder.Property(report => report.AppliedAt).HasColumnName("applied_at").HasColumnType("timestamp with time zone");
        builder.Property(report => report.ApplicationReason).HasColumnName("application_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(report => report.ApplicationEvidence).HasColumnName("application_evidence").HasColumnType("jsonb");
        builder.Property(report => report.Version).IsRowVersion();
        builder.Property(report => report.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(report => report.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(report => new { report.CallerScope, report.IdempotencyKey })
            .HasDatabaseName("uq_plant_inventory_change_reports_caller_idempotency")
            .IsUnique();
        builder.HasIndex(report => new { report.FarmId, report.ExistingPlantId })
            .HasDatabaseName("uq_plant_inventory_change_reports_open_plant")
            .HasFilter($"existing_plant_id IS NOT NULL AND status IN ({OpenStatuses})")
            .IsUnique();
        builder.HasIndex(report => new { report.FarmId, report.PoleLocationKey })
            .HasDatabaseName("uq_plant_inventory_change_reports_open_pole")
            .HasFilter($"status IN ({OpenStatuses})")
            .IsUnique();
        builder.HasIndex(report => new { report.FarmId, report.Status, report.CreatedAt })
            .HasDatabaseName("ix_plant_inventory_change_reports_review_queue");
        builder.HasIndex(report => new { report.TenantId, report.CreatedAt })
            .HasDatabaseName("ix_plant_inventory_change_reports_owner_history")
            .IsDescending(false, true);
        builder.HasIndex(report => report.ReportedLocation)
            .HasDatabaseName("ix_plant_inventory_change_reports_location_gist")
            .HasMethod("gist");

        builder.HasOne<Plant>()
            .WithMany()
            .HasForeignKey(report => new { report.ExistingPlantId, report.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_inventory_change_reports_existing_plant_same_farm");
        builder.HasOne<Plant>()
            .WithMany()
            .HasForeignKey(report => new { report.ResultingPlantId, report.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_inventory_change_reports_resulting_plant_same_farm");
    }
}
