using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Verifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class ConditionDetectionReviewConfiguration
    : IEntityTypeConfiguration<ConditionDetectionReview>
{
    public void Configure(EntityTypeBuilder<ConditionDetectionReview> builder)
    {
        builder.ToTable(
            "condition_detection_reviews",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Immutable human review items that preserve the original condition prediction.");
                tableBuilder.HasCheckConstraint(
                    "ck_condition_review_correction_values",
                    "(decision = 'CORRECTED'::system.condition_review_decision AND " +
                    "(corrected_condition_id IS NOT NULL OR corrected_severity_level_id IS NOT NULL)) OR " +
                    "(decision IN ('CONFIRMED'::system.condition_review_decision, " +
                    "'REJECTED'::system.condition_review_decision) AND " +
                    "corrected_condition_id IS NULL AND corrected_severity_level_id IS NULL)");
            });

        builder.HasKey(review => review.Id).HasName("pk_condition_detection_reviews");

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(review => review.ScanVerificationId).HasColumnName("scan_verification_id").HasColumnType("uuid");
        builder.Property(review => review.PlantScanId).HasColumnName("plant_scan_id").HasColumnType("uuid");
        builder.Property(review => review.ConditionDetectionId).HasColumnName("condition_detection_id").HasColumnType("uuid");
        builder.Property(review => review.Decision).HasColumnName("decision").HasColumnType("system.condition_review_decision").IsRequired();
        builder.Property(review => review.CorrectedConditionId).HasColumnName("corrected_condition_id").HasColumnType("uuid");
        builder.Property(review => review.CorrectedSeverityLevelId).HasColumnName("corrected_severity_level_id").HasColumnType("uuid");
        builder.Property(review => review.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(review => review.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(review => new { review.ScanVerificationId, review.ConditionDetectionId })
            .HasDatabaseName("uq_condition_reviews_verification_detection")
            .IsUnique();

        builder.HasOne<ScanVerification>()
            .WithMany()
            .HasForeignKey(review => new { review.ScanVerificationId, review.PlantScanId })
            .HasPrincipalKey(verification => new { verification.Id, verification.PlantScanId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_condition_reviews_verification_same_scan");

        builder.HasOne<ConditionDetection>()
            .WithMany()
            .HasForeignKey(review => new { review.ConditionDetectionId, review.PlantScanId })
            .HasPrincipalKey(detection => new { detection.Id, detection.PlantScanId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_reviews_detection_same_scan");

        builder.HasOne<PlantCondition>()
            .WithMany()
            .HasForeignKey(review => review.CorrectedConditionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_reviews_corrected_condition_id");

        builder.HasOne<HealthLevel>()
            .WithMany()
            .HasForeignKey(review => review.CorrectedSeverityLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_reviews_corrected_severity_level_id");
    }
}
