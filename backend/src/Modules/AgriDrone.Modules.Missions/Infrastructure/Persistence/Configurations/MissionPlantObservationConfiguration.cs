using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionPlantObservationConfiguration
    : IEntityTypeConfiguration<MissionPlantObservation>
{
    public void Configure(EntityTypeBuilder<MissionPlantObservation> builder)
    {
        builder.ToTable(
            "mission_plant_observations",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Intermediate AI detections from mapping/inspection used to match a detected object to an existing Plant ID.");
                tableBuilder.HasCheckConstraint(
                    "ck_observation_detection_confidence",
                    "detection_confidence IS NULL OR detection_confidence BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_observation_match_confidence",
                    "match_confidence IS NULL OR match_confidence BETWEEN 0 AND 1");
            });

        builder.HasKey(observation => observation.Id)
            .HasName("pk_mission_plant_observations");

        builder.Property(observation => observation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(observation => observation.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.AiJobId)
            .HasColumnName("ai_job_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.ModelVersionId)
            .HasColumnName("model_version_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.TrackingId)
            .HasColumnName("tracking_id")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(observation => observation.DetectedLocation)
            .HasColumnName("detected_location")
            .HasColumnType("geometry(Point,4326)");

        builder.Property(observation => observation.DetectionConfidence)
            .HasColumnName("detection_confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(observation => observation.SuggestedPlantId)
            .HasColumnName("suggested_plant_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.MatchConfidence)
            .HasColumnName("match_confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(observation => observation.ResolvedPlantId)
            .HasColumnName("resolved_plant_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.ReviewStatus)
            .HasColumnName("review_status")
            .HasColumnType("system.observation_review_status")
            .HasDefaultValueSql("'PENDING'::system.observation_review_status")
            .IsRequired();

        builder.Property(observation => observation.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");

        builder.Property(observation => observation.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(observation => observation.EvidenceMediaId)
            .HasColumnName("evidence_media_id")
            .HasColumnType("uuid");

        builder.Property(observation => observation.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(observation => new { observation.MissionId, observation.TrackingId })
            .HasDatabaseName("ux_observation_mission_tracking")
            .HasFilter("tracking_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(observation => observation.MissionId)
            .HasDatabaseName("ix_observation_mission");

        builder.HasIndex(observation => observation.ResolvedPlantId)
            .HasDatabaseName("ix_observation_resolved_plant");

        builder.HasIndex(observation => observation.DetectedLocation)
            .HasDatabaseName("ix_observation_location_gist")
            .HasMethod("gist");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(observation => new { observation.MissionId, observation.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_observation_mission_same_farm");

        builder.HasOne<AiProcessingJob>()
            .WithMany()
            .HasForeignKey(observation => observation.AiJobId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_observations_ai_jobs_ai_job_id");

        builder.HasOne<AiModelVersion>()
            .WithMany()
            .HasForeignKey(observation => observation.ModelVersionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_observations_ai_models_model_version_id");

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(observation => observation.EvidenceMediaId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_observations_media_assets_evidence_media_id");
    }
}
