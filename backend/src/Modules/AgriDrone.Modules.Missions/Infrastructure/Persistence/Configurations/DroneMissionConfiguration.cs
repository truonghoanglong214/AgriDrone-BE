using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class DroneMissionConfiguration : IEntityTypeConfiguration<DroneMission>
{
    public void Configure(EntityTypeBuilder<DroneMission> builder)
    {
        builder.ToTable(
            "drone_missions",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Drone flight mission for mapping or health inspection, including route and processing state.");
                tableBuilder.HasCheckConstraint(
                    "ck_drone_missions_time",
                    "ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at");
                tableBuilder.HasCheckConstraint(
                    "ck_drone_missions_detected_count",
                    "detected_plant_count IS NULL OR detected_plant_count >= 0");
            });

        builder.HasKey(mission => mission.Id).HasName("pk_drone_missions");
        builder.HasAlternateKey(mission => new { mission.Id, mission.FarmId })
            .HasName("uq_drone_missions_id_farm");
        builder.HasAlternateKey(mission => new { mission.Id, mission.TenantId })
            .HasName("uq_drone_missions_id_tenant");

        builder.Property(mission => mission.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(mission => mission.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.DroneId)
            .HasColumnName("drone_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.PilotUserId)
            .HasColumnName("pilot_user_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.MissionCode)
            .HasColumnName("mission_code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(mission => mission.MissionType)
            .HasColumnName("mission_type")
            .HasColumnType("system.mission_type")
            .IsRequired();

        builder.Property(mission => mission.Status)
            .HasColumnName("status")
            .HasColumnType("system.mission_status")
            .HasDefaultValueSql("'DRAFT'::system.mission_status")
            .IsRequired();

        builder.Property(mission => mission.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasColumnType("system.processing_status")
            .HasDefaultValueSql("'NOT_UPLOADED'::system.processing_status")
            .IsRequired();

        builder.Property(mission => mission.ScheduledAt)
            .HasColumnName("scheduled_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(mission => mission.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(mission => mission.EndedAt)
            .HasColumnName("ended_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(mission => mission.FlightRoute)
            .HasColumnName("flight_route")
            .HasColumnType("geometry(LineString,4326)");

        builder.Property(mission => mission.FlightParameters)
            .HasColumnName("flight_parameters")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(mission => mission.DetectedPlantCount)
            .HasColumnName("detected_plant_count")
            .HasColumnType("integer");

        builder.Property(mission => mission.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(mission => mission.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(mission => mission.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(mission => mission.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(mission => new { mission.FarmId, mission.MissionCode })
            .HasDatabaseName("uq_drone_missions_farm_code")
            .IsUnique();

        builder.HasIndex(mission => mission.TenantId)
            .HasDatabaseName("ix_drone_missions_tenant");

        builder.HasIndex(mission => new { mission.FarmId, mission.StartedAt })
            .HasDatabaseName("ix_drone_missions_farm_started")
            .IsDescending(false, true);

        builder.HasIndex(mission => new { mission.DroneId, mission.StartedAt })
            .HasDatabaseName("ix_drone_missions_drone_started")
            .IsDescending(false, true);

        builder.HasIndex(mission => new { mission.Status, mission.ProcessingStatus })
            .HasDatabaseName("ix_drone_missions_status");

        builder.HasIndex(mission => mission.FlightRoute)
            .HasDatabaseName("ix_drone_missions_route_gist")
            .HasMethod("gist");

        builder.HasOne(mission => mission.Drone)
            .WithMany(drone => drone.Missions)
            .HasForeignKey(mission => new { mission.DroneId, mission.TenantId })
            .HasPrincipalKey(drone => new { drone.Id, drone.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_drones_same_tenant");
    }
}
