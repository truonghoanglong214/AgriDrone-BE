using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MediaUploadSessionConfiguration
    : IEntityTypeConfiguration<MediaUploadSession>
{
    public void Configure(
        EntityTypeBuilder<MediaUploadSession> builder)
    {
        builder.ToTable(
            "media_upload_sessions",
            "mission",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_upload_sessions_file_size",
                    "file_size_bytes > 0");

                table.HasCheckConstraint(
                    "ck_upload_sessions_expiry",
                    "expires_at > created_at");

                table.HasCheckConstraint(
                    "ck_upload_sessions_updated_at",
                    "updated_at >= created_at");

                table.HasCheckConstraint(
                    "ck_upload_sessions_checksum",
                    "checksum_algorithm = 'SHA256' AND " +
                    "expected_checksum ~ '^[0-9a-f]{64}$'");

                table.HasCheckConstraint(
                    "ck_upload_sessions_status",
                    "status IN (" +
                    "'Pending', 'Verifying', 'Completed', " +
                    "'Failed', 'Expired', 'Aborted')");
            });

        builder.HasKey(session => session.Id)
            .HasName("pk_media_upload_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(session => session.FarmId)
            .HasColumnName("farm_id");

        builder.Property(session => session.MissionId)
            .HasColumnName("mission_id");

        builder.Property(session => session.OperationId)
            .HasColumnName("operation_id");

        builder.Property(session => session.MediaAssetId)
            .HasColumnName("media_asset_id");

        builder.Property(session => session.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(session => session.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(session => session.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(session => session.MediaType)
            .HasColumnName("media_type")
            .HasColumnType("system.media_type")
            .IsRequired();

        builder.Property(session => session.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(session => session.ChecksumAlgorithm)
            .HasColumnName("checksum_algorithm")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(session => session.ExpectedChecksum)
            .HasColumnName("expected_checksum")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(session => session.StorageUri)
            .HasColumnName("storage_uri")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(session => session.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(session => session.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(session => session.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(session => session.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(session => session.Version)
            .IsRowVersion();

        builder.HasIndex(session => new
        {
            session.TenantId,
            session.MissionId,
            session.OperationId
        })
            .IsUnique()
            .HasDatabaseName("uq_upload_sessions_operation");

        builder.HasIndex(session => session.MediaAssetId)
            .IsUnique()
            .HasDatabaseName("uq_upload_sessions_media_asset");

        builder.HasIndex(session => session.StorageUri)
            .IsUnique()
            .HasDatabaseName("uq_upload_sessions_storage_uri");

        builder.HasIndex(session => new
        {
            session.Status,
            session.ExpiresAt
        })
            .HasDatabaseName("ix_upload_sessions_cleanup");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(session => new
            {
                session.MissionId,
                session.FarmId
            })
            .HasPrincipalKey(mission => new
            {
                mission.Id,
                mission.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_upload_sessions_mission_farm");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(session => new
            {
                session.MissionId,
                session.TenantId
            })
            .HasPrincipalKey(mission => new
            {
                mission.Id,
                mission.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_upload_sessions_mission_tenant");
    }
}