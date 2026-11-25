using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CaptureInputManifestFileConfiguration : IEntityTypeConfiguration<CaptureInputManifestFile>
{
    public void Configure(EntityTypeBuilder<CaptureInputManifestFile> builder)
    {
        builder.ToTable(
            "capture_input_manifest_files",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_capture_input_manifest_files_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("ck_capture_input_manifest_files_size", "size_bytes > 0");
            });

        builder.Ignore(file => file.Id);

        builder.HasKey(file => new { file.ManifestId, file.MediaAssetId })
            .HasName("pk_capture_input_manifest_files");

        builder.Property(file => file.ManifestId)
            .HasColumnName("manifest_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(file => file.MediaAssetId)
            .HasColumnName("media_asset_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(file => file.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("character varying(64)")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(file => file.SizeBytes)
            .HasColumnName("size_bytes")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(file => file.MediaRole)
            .HasColumnName("media_role")
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(file => file.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(file => file.ManifestId)
            .HasDatabaseName("ix_capture_input_manifest_files_manifest");

        builder.HasIndex(file => file.MediaAssetId)
            .HasDatabaseName("ix_capture_input_manifest_files_media_asset");

        builder.HasOne<CaptureInputManifest>()
            .WithMany()
            .HasForeignKey(file => file.ManifestId)
            .HasPrincipalKey(manifest => manifest.Id)
            .HasConstraintName("fk_capture_input_manifest_files_manifest")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
