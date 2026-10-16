using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class BlackboxDecodeConfiguration : IEntityTypeConfiguration<BlackboxDecode>
{
    public void Configure(EntityTypeBuilder<BlackboxDecode> builder)
    {
        builder.ToTable("blackbox_decodes", "mission", table =>
            table.HasCheckConstraint("ck_blackbox_decodes_points_object",
                "jsonb_typeof(points_json) = 'object'"));
        builder.HasKey(decode => decode.Id);
        builder.Property(decode => decode.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(decode => decode.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(decode => decode.FarmId).HasColumnName("farm_id").IsRequired();
        builder.Property(decode => decode.MissionId).HasColumnName("mission_id").IsRequired();
        builder.Property(decode => decode.SourceFileName).HasColumnName("source_file_name")
            .HasMaxLength(255).IsRequired();
        builder.Property(decode => decode.SourceChecksum).HasColumnName("source_checksum")
            .HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(decode => decode.PointsJson).HasColumnName("points_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(decode => decode.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(decode => decode.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(decode => new { decode.MissionId, decode.SourceChecksum })
            .IsUnique().HasDatabaseName("uq_blackbox_decodes_mission_checksum");
        builder.HasOne<DroneMission>().WithMany()
            .HasForeignKey(decode => new { decode.MissionId, decode.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DroneMission>().WithMany()
            .HasForeignKey(decode => new { decode.MissionId, decode.TenantId })
            .HasPrincipalKey(mission => new { mission.Id, mission.TenantId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
