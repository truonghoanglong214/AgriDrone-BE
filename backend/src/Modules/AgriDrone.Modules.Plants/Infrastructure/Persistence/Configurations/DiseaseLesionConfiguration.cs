using AgriDrone.Modules.Plants.Domain.Diseases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class DiseaseLesionConfiguration : IEntityTypeConfiguration<DiseaseLesion>
{
    public void Configure(EntityTypeBuilder<DiseaseLesion> builder)
    {
        builder.ToTable(
            "disease_lesions",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Individual disease bounding boxes/localized affected areas on an image.");
                tableBuilder.HasCheckConstraint(
                    "ck_lesion_bbox_range",
                    "x_min BETWEEN 0 AND 1 AND y_min BETWEEN 0 AND 1 AND " +
                    "x_max BETWEEN 0 AND 1 AND y_max BETWEEN 0 AND 1 AND " +
                    "x_min < x_max AND y_min < y_max");
                tableBuilder.HasCheckConstraint(
                    "ck_lesion_confidence",
                    "confidence IS NULL OR confidence BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_lesion_affected_ratio",
                    "affected_ratio IS NULL OR affected_ratio BETWEEN 0 AND 1");
            });

        builder.HasKey(lesion => lesion.Id).HasName("pk_disease_lesions");

        builder.Property(lesion => lesion.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(lesion => lesion.DiseaseDetectionId)
            .HasColumnName("disease_detection_id")
            .HasColumnType("uuid");

        builder.Property(lesion => lesion.MediaId)
            .HasColumnName("media_id")
            .HasColumnType("uuid");

        builder.Property(lesion => lesion.XMin)
            .HasColumnName("x_min")
            .HasColumnType("numeric(8,7)")
            .HasPrecision(8, 7);

        builder.Property(lesion => lesion.YMin)
            .HasColumnName("y_min")
            .HasColumnType("numeric(8,7)")
            .HasPrecision(8, 7);

        builder.Property(lesion => lesion.XMax)
            .HasColumnName("x_max")
            .HasColumnType("numeric(8,7)")
            .HasPrecision(8, 7);

        builder.Property(lesion => lesion.YMax)
            .HasColumnName("y_max")
            .HasColumnType("numeric(8,7)")
            .HasPrecision(8, 7);

        builder.Property(lesion => lesion.Confidence)
            .HasColumnName("confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(lesion => lesion.AffectedRatio)
            .HasColumnName("affected_ratio")
            .HasColumnType("numeric(6,5)")
            .HasPrecision(6, 5);

        builder.Property(lesion => lesion.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne<DiseaseDetection>()
            .WithMany()
            .HasForeignKey(lesion => lesion.DiseaseDetectionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_disease_lesions_detections_detection_id");
    }
}
