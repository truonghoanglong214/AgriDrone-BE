using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class AiDetectionThresholdConfiguration : IEntityTypeConfiguration<AiDetectionThreshold>
{
    public void Configure(EntityTypeBuilder<AiDetectionThreshold> builder)
    {
        builder.ToTable(
            "ai_detection_thresholds",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Per-condition confidence thresholds belonging to a versioned AI threshold profile.");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_detection_threshold_confidence",
                    "min_confidence BETWEEN 0 AND 1");
            });

        builder.HasKey(threshold => threshold.Id).HasName("pk_ai_detection_thresholds");

        builder.Property(threshold => threshold.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(threshold => threshold.ThresholdProfileId).HasColumnName("threshold_profile_id").HasColumnType("uuid");
        builder.Property(threshold => threshold.ConditionId).HasColumnName("condition_id").HasColumnType("uuid");
        builder.Property(threshold => threshold.MinConfidence).HasColumnName("min_confidence").HasColumnType("numeric(5,4)").HasPrecision(5, 4);
        builder.Property(threshold => threshold.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(threshold => new { threshold.ThresholdProfileId, threshold.ConditionId })
            .HasDatabaseName("uq_ai_detection_thresholds_profile_condition")
            .IsUnique();

        builder.HasOne<AiThresholdProfile>()
            .WithMany()
            .HasForeignKey(threshold => threshold.ThresholdProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ai_detection_thresholds_profiles_profile_id");
    }
}
