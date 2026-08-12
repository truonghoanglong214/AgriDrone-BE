using AgriDrone.Modules.Missions.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable(
            "media_assets",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Metadata for files stored in Cloudinary/S3-compatible object storage; binary data is not stored in PostgreSQL.");
                tableBuilder.HasCheckConstraint(
                    "ck_media_size",
                    "file_size_bytes IS NULL OR file_size_bytes >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_media_dimensions",
                    "(width_px IS NULL OR width_px > 0) AND " +
                    "(height_px IS NULL OR height_px > 0) AND " +
                    "(duration_ms IS NULL OR duration_ms >= 0)");
            });

        builder.HasKey(media => media.Id).HasName("pk_media_assets");

        builder.Property(media => media.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(media => media.Provider)
            .HasColumnName("provider")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(media => media.StorageKey)
            .HasColumnName("storage_key")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(media => media.Url)
            .HasColumnName("url")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(media => media.MediaType)
            .HasColumnName("media_type")
            .HasColumnType("system.media_type")
            .IsRequired();

        builder.Property(media => media.MimeType)
            .HasColumnName("mime_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(media => media.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .HasColumnType("bigint");

        builder.Property(media => media.WidthPx)
            .HasColumnName("width_px")
            .HasColumnType("integer");

        builder.Property(media => media.HeightPx)
            .HasColumnName("height_px")
            .HasColumnType("integer");

        builder.Property(media => media.DurationMs)
            .HasColumnName("duration_ms")
            .HasColumnType("bigint");

        builder.Property(media => media.Checksum)
            .HasColumnName("checksum")
            .HasColumnType("character varying(128)")
            .HasMaxLength(128);

        builder.Property(media => media.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(media => media.UploadedBy)
            .HasColumnName("uploaded_by")
            .HasColumnType("uuid");

        builder.Property(media => media.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(media => media.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(media => new { media.Provider, media.StorageKey })
            .HasDatabaseName("uq_media_assets_provider_storage_key")
            .IsUnique();

        builder.HasIndex(media => media.CreatedAt)
            .HasDatabaseName("ix_media_assets_created")
            .IsDescending();
    }
}
