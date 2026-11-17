using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyOrderPoleCountSnapshotItemConfiguration
    : IEntityTypeConfiguration<SurveyOrderPoleCountSnapshotItem>
{
    public void Configure(EntityTypeBuilder<SurveyOrderPoleCountSnapshotItem> builder)
    {
        builder.ToTable("survey_order_pole_count_snapshot_items", "survey");

        builder.HasKey(i => i.Id).HasName("pk_survey_order_pole_count_snapshot_items");
        builder.Property(i => i.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(i => i.SnapshotId).HasColumnName("snapshot_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.MappingCandidateBatchId).HasColumnName("mapping_candidate_batch_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.MappingCandidateId).HasColumnName("mapping_candidate_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.PlantProfileId).HasColumnName("plant_profile_id").HasColumnType("uuid");
        builder.Property(i => i.EffectivePosition).HasColumnName("effective_position").HasColumnType("geometry(Point, 4326)").IsRequired();
        builder.Property(i => i.ReviewDecision).HasColumnName("review_decision").HasMaxLength(50).IsRequired();
        builder.Property(i => i.IsAccepted).HasColumnName("is_accepted").HasColumnType("boolean").IsRequired();
        builder.Property(i => i.IsStrictlyInBoundary).HasColumnName("is_strictly_in_boundary").HasColumnType("boolean").IsRequired();
        builder.Property(i => i.ExclusionReason).HasColumnName("exclusion_reason").HasMaxLength(500);

        builder.HasIndex(i => new { i.SnapshotId, i.MappingCandidateId })
            .IsUnique()
            .HasDatabaseName("uq_survey_order_pole_count_snapshot_items_snapshot_candidate");

        builder.HasIndex(i => i.EffectivePosition)
            .HasMethod("gist")
            .HasDatabaseName("ix_survey_order_pole_count_snapshot_items_pos_gist");
    }
}
