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
            tableBuilder => tableBuilder.HasComment(
                "Immutable verification history for manager/worker confirmation or rejection of scan results."));

        builder.HasKey(verification => verification.Id).HasName("pk_scan_verifications");

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

        builder.Property(verification => verification.CorrectedHealthStatus)
            .HasColumnName("corrected_health_status")
            .HasColumnType("system.health_status");

        builder.Property(verification => verification.Note)
            .HasColumnName("note")
            .HasColumnType("text");

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

        builder.HasOne<PlantScan>()
            .WithMany()
            .HasForeignKey(verification => verification.PlantScanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scan_verifications_plant_scans_scan_id");
    }
}
