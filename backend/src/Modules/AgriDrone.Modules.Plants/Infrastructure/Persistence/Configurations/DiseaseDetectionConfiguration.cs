using AgriDrone.Modules.Plants.Domain.Diseases;
using AgriDrone.Modules.Plants.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class DiseaseDetectionConfiguration : IEntityTypeConfiguration<DiseaseDetection>
{
    public void Configure(EntityTypeBuilder<DiseaseDetection> builder)
    {
        builder.ToTable(
            "disease_detections",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Disease findings for a plant scan, including confidence, severity, AI model and review state.");
                tableBuilder.HasCheckConstraint(
                    "ck_detection_confidence",
                    "confidence IS NULL OR confidence BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_detection_lesion_count",
                    "lesion_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_detection_affected_ratio",
                    "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");
            });

        builder.HasKey(detection => detection.Id).HasName("pk_disease_detections");

        builder.Property(detection => detection.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(detection => detection.PlantScanId)
            .HasColumnName("plant_scan_id")
            .HasColumnType("uuid");

        builder.Property(detection => detection.DiseaseId)
            .HasColumnName("disease_id")
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

        builder.Property(detection => detection.Severity)
            .HasColumnName("severity")
            .HasColumnType("system.disease_severity")
            .IsRequired();

        builder.Property(detection => detection.LesionCount)
            .HasColumnName("lesion_count")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(detection => detection.AffectedRatio)
            .HasColumnName("affected_ratio")
            .HasColumnType("numeric(6,5)")
            .HasPrecision(6, 5);

        builder.Property(detection => detection.ReviewStatus)
            .HasColumnName("review_status")
            .HasColumnType("system.review_status")
            .HasDefaultValueSql("'PENDING'::system.review_status")
            .IsRequired();

        builder.Property(detection => detection.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");

        builder.Property(detection => detection.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(detection => detection.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(detection => new { detection.PlantScanId, detection.DiseaseId })
            .HasDatabaseName("uq_detection_scan_disease")
            .IsUnique();

        builder.HasIndex(detection => detection.PlantScanId)
            .HasDatabaseName("ix_disease_detections_scan");

        builder.HasIndex(detection => detection.DiseaseId)
            .HasDatabaseName("ix_disease_detections_disease");

        builder.HasIndex(detection => detection.ReviewStatus)
            .HasDatabaseName("ix_disease_detections_review");

        builder.HasOne<PlantScan>()
            .WithMany()
            .HasForeignKey(detection => detection.PlantScanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_disease_detections_plant_scans_scan_id");

        builder.HasOne<Disease>()
            .WithMany()
            .HasForeignKey(detection => detection.DiseaseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_disease_detections_diseases_disease_id");
    }
}
