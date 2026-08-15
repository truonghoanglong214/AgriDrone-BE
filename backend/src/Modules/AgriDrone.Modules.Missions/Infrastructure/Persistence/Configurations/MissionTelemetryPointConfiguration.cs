using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionTelemetryPointConfiguration
    : IEntityTypeConfiguration<MissionTelemetryPoint>
{
    public void Configure(EntityTypeBuilder<MissionTelemetryPoint> builder)
    {
        builder.ToTable(
            "mission_telemetry_points",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Timestamped drone telemetry used to interpolate frame and detection locations.");
                tableBuilder.HasCheckConstraint(
                    "ck_mission_telemetry_sequence_nonnegative",
                    "sequence_number >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_mission_telemetry_heading_range",
                    "heading_deg IS NULL OR (heading_deg >= 0 AND heading_deg < 360)");
                tableBuilder.HasCheckConstraint(
                    "ck_mission_telemetry_speed_nonnegative",
                    "speed_mps IS NULL OR speed_mps >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_mission_telemetry_accuracy_nonnegative",
                    "horizontal_accuracy_m IS NULL OR horizontal_accuracy_m >= 0");
            });

        builder.HasKey(point => point.Id).HasName("pk_mission_telemetry_points");

        builder.Property(point => point.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(point => point.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(point => point.SequenceNumber)
            .HasColumnName("sequence_number")
            .HasColumnType("integer");

        builder.Property(point => point.RecordedAt)
            .HasColumnName("recorded_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(point => point.Location)
            .HasColumnName("location")
            .HasColumnType("geometry(Point,4326)")
            .IsRequired();

        builder.Property(point => point.AltitudeM)
            .HasColumnName("altitude_m")
            .HasColumnType("numeric(9,3)")
            .HasPrecision(9, 3);

        builder.Property(point => point.AltitudeReference)
            .HasColumnName("altitude_reference")
            .HasColumnType("system.altitude_reference");

        builder.Property(point => point.HeadingDeg)
            .HasColumnName("heading_deg")
            .HasColumnType("numeric(6,2)")
            .HasPrecision(6, 2);

        builder.Property(point => point.SpeedMps)
            .HasColumnName("speed_mps")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(point => point.HorizontalAccuracyM)
            .HasColumnName("horizontal_accuracy_m")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(point => point.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(point => new { point.MissionId, point.SequenceNumber })
            .HasDatabaseName("uq_mission_telemetry_mission_sequence")
            .IsUnique();

        builder.HasIndex(point => new { point.MissionId, point.RecordedAt })
            .HasDatabaseName("uq_mission_telemetry_mission_recorded_at")
            .IsUnique();

        builder.HasIndex(point => point.Location)
            .HasDatabaseName("ix_mission_telemetry_location_gist")
            .HasMethod("gist");

        builder.HasOne(point => point.Mission)
            .WithMany(mission => mission.TelemetryPoints)
            .HasForeignKey(point => point.MissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mission_telemetry_points_missions_mission_id");
    }
}
