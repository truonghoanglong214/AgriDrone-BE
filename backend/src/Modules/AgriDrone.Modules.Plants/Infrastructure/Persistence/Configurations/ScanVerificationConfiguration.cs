using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.Modules.Plants.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class ScanVerificationConfiguration : IEntityTypeConfiguration<ScanVerification>
{
    public void Configure(EntityTypeBuilder<ScanVerification> builder)
    {
        builder.ToTable(
            "scan_verifications",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Immutable revisioned review sessions for manager/worker verification of scan results.");
                tableBuilder.HasCheckConstraint(
                    "ck_scan_verification_revision_positive",
                    "revision_number >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_scan_verification_revision_chain",
                    "(revision_number = 1 AND supersedes_verification_id IS NULL) OR " +
                    "(revision_number > 1 AND supersedes_verification_id IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_scan_verification_target_decision",
                    "decision IN ('CONFIRMED'::system.verification_decision, " +
                    "'CORRECTED'::system.verification_decision, " +
                    "'REJECTED'::system.verification_decision, " +
                    "'FIELD_INSPECTION_REQUIRED'::system.verification_decision)");
            });

        builder.HasKey(verification => verification.Id).HasName("pk_scan_verifications");
        builder.HasAlternateKey(verification => new { verification.Id, verification.PlantScanId })
            .HasName("uq_scan_verifications_id_scan");

        builder.Property(verification => verification.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(verification => verification.PlantScanId)
            .HasColumnName("plant_scan_id")
            .HasColumnType("uuid");

        builder.Property(verification => verification.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(verification => verification.Decision)
            .HasColumnName("decision")
            .HasColumnType("system.verification_decision")
            .IsRequired();

        builder.Property(verification => verification.CorrectedHealthLevelId)
            .HasColumnName("corrected_health_level_id")
            .HasColumnType("uuid");

        builder.Property(verification => verification.Note)
            .HasColumnName("note")
            .HasColumnType("text");

        builder.Property(verification => verification.RevisionNumber)
            .HasColumnName("revision_number")
            .HasColumnType("integer");

        builder.Property(verification => verification.SupersedesVerificationId)
            .HasColumnName("supersedes_verification_id")
            .HasColumnType("uuid");

        builder.Property(verification => verification.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(verification => new
        {
            verification.PlantScanId,
            verification.CreatedAt
        })
            .HasDatabaseName("ix_scan_verifications_scan")
            .IsDescending(false, true);

        builder.HasIndex(verification => new
        {
            verification.PlantScanId,
            verification.RevisionNumber
        })
            .HasDatabaseName("uq_scan_verifications_scan_revision")
            .IsUnique();

        builder.HasOne<PlantScan>()
            .WithMany()
            .HasForeignKey(verification => verification.PlantScanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scan_verifications_plant_scans_scan_id");

        builder.HasOne<ScanVerification>()
            .WithMany()
            .HasForeignKey(verification => new
            {
                verification.SupersedesVerificationId,
                verification.PlantScanId
            })
            .HasPrincipalKey(previous => new { previous.Id, previous.PlantScanId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_scan_verifications_supersedes_same_scan");

        builder.HasOne<HealthLevel>()
            .WithMany()
            .HasForeignKey(verification => verification.CorrectedHealthLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_scan_verifications_health_levels_corrected_health_level_id");
    }
}
