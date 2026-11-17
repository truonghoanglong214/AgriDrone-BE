using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyOrderPoleCountSnapshotConfiguration
    : IEntityTypeConfiguration<SurveyOrderPoleCountSnapshot>
{
    public void Configure(EntityTypeBuilder<SurveyOrderPoleCountSnapshot> builder)
    {
        builder.ToTable(
            "survey_order_pole_count_snapshots",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_order_pole_count_snapshots_count", "verified_billable_pole_count >= 0");
                table.HasCheckConstraint("ck_survey_order_pole_count_snapshots_rev", "revision_number >= 1");
            });

        builder.HasKey(s => s.Id).HasName("pk_survey_order_pole_count_snapshots");
        builder.HasAlternateKey(s => new { s.Id, s.TenantId, s.FarmId }).HasName("uq_survey_order_pole_count_snapshots_id_tenant_farm");

        builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(s => s.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.MissionId).HasColumnName("mission_id").HasColumnType("uuid");
        builder.Property(s => s.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid");
        builder.Property(s => s.FarmBaseMapVersionId).HasColumnName("farm_base_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.MappingCandidateBatchId).HasColumnName("mapping_candidate_batch_id").HasColumnType("uuid");
        builder.Property(s => s.SpatialPolicyVersionId).HasColumnName("spatial_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.VerifiedBillablePoleCount).HasColumnName("verified_billable_pole_count").HasColumnType("integer").IsRequired();
        builder.Property(s => s.VerifiedBy).HasColumnName("verified_by").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.VerifiedAt).HasColumnName("verified_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(s => s.EvidenceFingerprint).HasColumnName("evidence_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(s => s.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").IsRequired();
        builder.Property(s => s.PredecessorSnapshotId).HasColumnName("predecessor_snapshot_id").HasColumnType("uuid");

        builder.HasIndex(s => new { s.SurveyOrderId, s.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("uq_survey_order_pole_count_snapshots_order_revision");

        builder.HasIndex(s => new { s.SurveyOrderId, s.EvidenceFingerprint })
            .IsUnique()
            .HasDatabaseName("uq_survey_order_pole_count_snapshots_order_fingerprint");

        builder.HasIndex(s => s.FarmId)
            .HasDatabaseName("ix_survey_order_pole_count_snapshots_farm");

        builder.HasIndex(s => s.MappingCandidateBatchId)
            .HasDatabaseName("ix_survey_order_pole_count_snapshots_batch");

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_survey_order_pole_count_snapshot_items_snapshot");
    }
}
