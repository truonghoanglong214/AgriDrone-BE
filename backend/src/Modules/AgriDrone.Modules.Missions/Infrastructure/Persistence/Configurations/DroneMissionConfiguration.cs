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
                    "ended_at IS NULL OR started_at IS NULL OR " +
                    "ended_at >= started_at");

                tableBuilder.HasCheckConstraint(
                    "ck_drone_missions_schedule_time",
                    "scheduled_end_at IS NULL OR scheduled_at IS NULL OR " +
                    "scheduled_end_at > scheduled_at");

                tableBuilder.HasCheckConstraint(
                    "ck_drone_missions_detected_count",
                    "detected_plant_count IS NULL OR " +
                    "detected_plant_count >= 0");

                tableBuilder.HasCheckConstraint(
                    "ck_drone_missions_health_review_counts",
                    "health_review_total >= 0 AND " +
                    "health_review_pending >= 0 AND " +
                    "health_review_awaiting_field_verification >= 0 AND " +
                    "health_review_resolved >= 0 AND " +
                    "health_review_pending + " +
                    "health_review_awaiting_field_verification + " +
                    "health_review_resolved = health_review_total");
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
            .HasSentinel((MissionStatus)(-1))
            .HasDefaultValueSql("'DRAFT'::system.mission_status")
            .IsRequired();

        builder.Property(mission => mission.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasColumnType("system.processing_status")
            .HasSentinel((ProcessingStatus)(-1))
            .HasDefaultValueSql("'NOT_UPLOADED'::system.processing_status")
            .IsRequired();

        builder.Property(mission => mission.ScheduledAt)
            .HasColumnName("scheduled_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(mission => mission.ScheduledEndAt)
            .HasColumnName("scheduled_end_at")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(mission => new
        {
            mission.TenantId,
            mission.DroneId,
            mission.ScheduledAt,
            mission.ScheduledEndAt
        })
            .HasDatabaseName(
                "ix_drone_missions_drone_schedule");
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

        builder.Property(mission => mission.PublishedMapVersionId)
            .HasColumnName("published_map_version_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.MappingApprovalId)
            .HasColumnName("mapping_approval_id")
            .HasColumnType("uuid");

        builder.Property(mission => mission.MapPublishedAt)
            .HasColumnName("map_published_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(mission => mission.HealthReviewHandoffId)
    .HasColumnName("health_review_handoff_id")
    .HasColumnType("uuid");

        builder.Property(mission => mission.HealthReviewVersion)
            .HasColumnName("health_review_version")
            .HasColumnType("bigint");

        builder.Property(mission => mission.HealthReviewState)
            .HasColumnName("health_review_state")
            .HasConversion<string>()
            .HasColumnType("character varying(32)")
            .HasMaxLength(32);

        builder.Property(mission => mission.HealthReviewTotal)
            .HasColumnName("health_review_total")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(mission => mission.HealthReviewPending)
            .HasColumnName("health_review_pending")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(
                mission =>
                    mission.HealthReviewAwaitingFieldVerification)
            .HasColumnName(
                "health_review_awaiting_field_verification")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(mission => mission.HealthReviewResolved)
            .HasColumnName("health_review_resolved")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(mission => mission.HealthReviewChangedAt)
            .HasColumnName("health_review_changed_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(mission => mission.HealthReviewHandoffId)
            .HasDatabaseName(
                "ux_drone_missions_health_review_handoff")
            .HasFilter("health_review_handoff_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(mission => mission.MappingApprovalId)
            .HasDatabaseName("ux_drone_missions_mapping_approval")
            .HasFilter("mapping_approval_id IS NOT NULL")
            .IsUnique();

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
            .WithMany()
            .HasForeignKey(mission => new { mission.DroneId, mission.TenantId })
            .HasPrincipalKey(drone => new { drone.Id, drone.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_drones_same_tenant");
    }
}
