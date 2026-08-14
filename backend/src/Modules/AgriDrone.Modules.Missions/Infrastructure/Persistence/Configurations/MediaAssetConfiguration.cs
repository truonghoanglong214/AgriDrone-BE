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
            "media",
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
                tableBuilder.HasCheckConstraint(
                    "ck_media_retention_after_creation",
                    "retention_until IS NULL OR retention_until >= created_at");
                tableBuilder.HasCheckConstraint(
                    "ck_media_archive_after_creation",
                    "archived_at IS NULL OR archived_at >= created_at");
                tableBuilder.HasCheckConstraint(
                    "ck_media_deletion_timeline",
                    "deletion_requested_at IS NULL OR deleted_at IS NULL OR " +
                    "deleted_at >= deletion_requested_at");
                tableBuilder.HasCheckConstraint(
                    "ck_media_storage_status",
                    "(storage_status = 'DELETED'::system.media_storage_status AND " +
                    "deletion_requested_at IS NOT NULL AND deleted_at IS NOT NULL) OR " +
                    "(storage_status <> 'DELETED'::system.media_storage_status AND deleted_at IS NULL AND " +
                    "(storage_status NOT IN ('DELETE_PENDING'::system.media_storage_status, " +
                    "'DELETE_FAILED'::system.media_storage_status) OR deletion_requested_at IS NOT NULL) AND " +
                    "(storage_status <> 'ARCHIVED'::system.media_storage_status OR archived_at IS NOT NULL))");
            });

        builder.HasKey(media => media.Id).HasName("pk_media_assets");
        builder.HasAlternateKey(media => new { media.Id, media.TenantId })
            .HasName("uq_media_assets_id_tenant");

        builder.Property(media => media.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(media => media.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(media => media.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

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

        builder.Property(media => media.RetentionUntil)
            .HasColumnName("retention_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(media => media.ArchivedAt)
            .HasColumnName("archived_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(media => media.DeletionRequestedAt)
            .HasColumnName("deletion_requested_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(media => media.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(media => media.StorageStatus)
            .HasColumnName("storage_status")
            .HasColumnType("system.media_storage_status")
            .HasDefaultValueSql("'ACTIVE'::system.media_storage_status")
            .IsRequired();

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

        builder.HasIndex(media => new { media.TenantId, media.CreatedAt })
            .HasDatabaseName("ix_media_assets_tenant_created")
            .IsDescending(false, true);

        builder.HasIndex(media => new { media.FarmId, media.CreatedAt })
            .HasDatabaseName("ix_media_assets_farm_created")
            .IsDescending(false, true);

        builder.HasIndex(media => new { media.StorageStatus, media.RetentionUntil })
            .HasDatabaseName("ix_media_assets_retention_cleanup")
            .HasFilter("deleted_at IS NULL");
    }
}
