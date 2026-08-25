using AgriDrone.Modules.Plants.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantScanMediaConfiguration : IEntityTypeConfiguration<PlantScanMedia>
{
    public void Configure(EntityTypeBuilder<PlantScanMedia> builder)
    {
        builder.ToTable(
            "plant_scan_media",
            "plant",
            tableBuilder => tableBuilder.HasComment(
                "Images associated with a specific plant health scan."));

        builder.HasKey(media => new { media.PlantScanId, media.MediaId })
            .HasName("pk_plant_scan_media");

        builder.Property(media => media.PlantScanId)
            .HasColumnName("plant_scan_id")
            .HasColumnType("uuid");

        builder.Property(media => media.MediaId)
            .HasColumnName("media_id")
            .HasColumnType("uuid");

        builder.Property(media => media.MediaRole)
            .HasColumnName("media_role")
            .HasColumnType("system.scan_media_role")
            .HasSentinel((ScanMediaRole)(-1))
            .HasDefaultValueSql("'CONTEXT'::system.scan_media_role")
            .IsRequired();

        builder.Property(media => media.IsPrimary)
            .HasColumnName("is_primary")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(media => media.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne(media => media.PlantScan)
            .WithMany(scan => scan.Media)
            .HasForeignKey(media => media.PlantScanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_plant_scan_media_plant_scans_scan_id");
    }
}
