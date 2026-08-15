using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class ConditionDetectionConfiguration : IEntityTypeConfiguration<ConditionDetection>
{
    public void Configure(EntityTypeBuilder<ConditionDetection> builder)
    {
        builder.ToTable(
            "condition_detections",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Immutable AI/manual condition findings for a plant scan with reproducible threshold metadata.");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_detection_confidence",
                    "confidence IS NULL OR confidence BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_detection_threshold",
                    "threshold_used IS NULL OR threshold_used BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_detection_lesion_count",
                    "lesion_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_detection_affected_ratio",
                    "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_detection_manual_creator",
                    "source <> 'MANUAL'::system.finding_source OR created_by IS NOT NULL");
            });

        builder.HasKey(detection => detection.Id).HasName("pk_condition_detections");
        builder.HasAlternateKey(detection => new { detection.Id, detection.PlantScanId })
            .HasName("uq_condition_detections_id_scan");

        builder.Property(detection => detection.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(detection => detection.PlantScanId)
            .HasColumnName("plant_scan_id")
            .HasColumnType("uuid");

        builder.Property(detection => detection.ConditionId)
            .HasColumnName("condition_id")
            .HasColumnType("uuid");

        builder.Property(detection => detection.ModelVersionId)
            .HasColumnName("model_version_id")
            .HasColumnType("uuid");

        builder.Property(detection => detection.Source)
            .HasColumnName("source")
            .HasColumnType("system.finding_source")
            .HasDefaultValueSql("'AI'::system.finding_source")
            .IsRequired();

        builder.Property(detection => detection.Confidence)
            .HasColumnName("confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(detection => detection.SeverityLevelId)
            .HasColumnName("severity_level_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(detection => detection.ThresholdUsed)
            .HasColumnName("threshold_used")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(detection => detection.LesionCount)
            .HasColumnName("lesion_count")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(detection => detection.AffectedRatio)
            .HasColumnName("affected_ratio")
            .HasColumnType("numeric(6,5)")
            .HasPrecision(6, 5);

        builder.Property(detection => detection.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(detection => detection.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(detection => new { detection.PlantScanId, detection.ConditionId })
            .HasDatabaseName("uq_condition_detection_scan_condition")
            .IsUnique();

        builder.HasIndex(detection => detection.PlantScanId)
            .HasDatabaseName("ix_condition_detections_scan");

        builder.HasIndex(detection => detection.ConditionId)
            .HasDatabaseName("ix_condition_detections_condition");

        builder.HasIndex(detection => detection.SeverityLevelId)
            .HasDatabaseName("ix_condition_detections_severity_level");

        builder.HasOne(detection => detection.PlantScan)
            .WithMany(scan => scan.ConditionDetections)
            .HasForeignKey(detection => detection.PlantScanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_condition_detections_plant_scans_scan_id");

        builder.HasOne(detection => detection.Condition)
            .WithMany(condition => condition.Detections)
            .HasForeignKey(detection => detection.ConditionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_detections_conditions_condition_id");

        builder.HasOne(detection => detection.SeverityLevel)
            .WithMany(level => level.ConditionDetections)
            .HasForeignKey(detection => detection.SeverityLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_detections_health_levels_severity_level_id");
    }
}
