using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionMediaConfiguration : IEntityTypeConfiguration<MissionMedia>
{
    public void Configure(EntityTypeBuilder<MissionMedia> builder)
    {
        builder.ToTable(
            "mission_media",
            "mission",
            tableBuilder => tableBuilder.HasComment(
                "Links raw/processed images or videos to a drone mission."));

        builder.HasKey(media => new { media.MissionId, media.MediaId })
            .HasName("pk_mission_media");

        builder.Property(media => media.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(media => media.MediaId)
            .HasColumnName("media_id")
            .HasColumnType("uuid");

        builder.Property(media => media.MediaRole)
            .HasColumnName("media_role")
            .HasColumnType("system.mission_media_role")
            .IsRequired();

        builder.Property(media => media.CapturedAt)
            .HasColumnName("captured_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(media => media.TelemetryTimeOffsetMs)
            .HasColumnName("telemetry_time_offset_ms")
            .HasColumnType("bigint");

        builder.Property(media => media.CaptureClockSource)
            .HasColumnName("capture_clock_source")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(media => media.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(media => media.MissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mission_media_drone_missions_mission_id");

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(media => media.MediaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mission_media_media_assets_media_id");
    }
}
