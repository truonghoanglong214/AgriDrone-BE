using AgriDrone.Modules.Missions.Domain.Observations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class ObservationMatchCandidateConfiguration
    : IEntityTypeConfiguration<ObservationMatchCandidate>
{
    public void Configure(EntityTypeBuilder<ObservationMatchCandidate> builder)
    {
        builder.ToTable(
            "observation_match_candidates",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Ranked plant candidates retained per matching strategy for reproducible re-identification evaluation.");
                tableBuilder.HasCheckConstraint(
                    "ck_match_candidates_rank_positive",
                    "candidate_rank >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_match_candidates_gps_distance_nonnegative",
                    "gps_distance_m IS NULL OR gps_distance_m >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_match_candidates_grid_score",
                    "grid_score IS NULL OR grid_score BETWEEN 0 AND 1");
                tableBuilder.HasCheckConstraint(
                    "ck_match_candidates_final_score",
                    "final_score BETWEEN 0 AND 1");
            });

        builder.HasKey(candidate => candidate.Id).HasName("pk_observation_match_candidates");

        builder.Property(candidate => candidate.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(candidate => candidate.ObservationId)
            .HasColumnName("observation_id")
            .HasColumnType("uuid");

        builder.Property(candidate => candidate.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(candidate => candidate.PlantId)
            .HasColumnName("plant_id")
            .HasColumnType("uuid");

        builder.Property(candidate => candidate.Strategy)
            .HasColumnName("strategy")
            .HasColumnType("system.match_strategy")
            .IsRequired();

        builder.Property(candidate => candidate.CandidateRank)
            .HasColumnName("candidate_rank")
            .HasColumnType("integer");

        builder.Property(candidate => candidate.GpsDistanceM)
            .HasColumnName("gps_distance_m")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(candidate => candidate.RowDelta)
            .HasColumnName("row_delta")
            .HasColumnType("integer");

        builder.Property(candidate => candidate.ColumnDelta)
            .HasColumnName("column_delta")
            .HasColumnType("integer");

        builder.Property(candidate => candidate.GridScore)
            .HasColumnName("grid_score")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(candidate => candidate.FinalScore)
            .HasColumnName("final_score")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(candidate => candidate.AlgorithmVersion)
            .HasColumnName("algorithm_version")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(candidate => candidate.Parameters)
            .HasColumnName("parameters")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(candidate => candidate.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(candidate => new
        {
            candidate.ObservationId,
            candidate.Strategy,
            candidate.PlantId
        })
            .HasDatabaseName("uq_match_candidates_observation_strategy_plant")
            .IsUnique();

        builder.HasIndex(candidate => new
        {
            candidate.ObservationId,
            candidate.Strategy,
            candidate.CandidateRank
        })
            .HasDatabaseName("uq_match_candidates_observation_strategy_rank")
            .IsUnique();

        builder.HasIndex(candidate => candidate.PlantId)
            .HasDatabaseName("ix_match_candidates_plant");

        builder.HasOne(candidate => candidate.Observation)
            .WithMany(observation => observation.MatchCandidates)
            .HasForeignKey(candidate => new { candidate.ObservationId, candidate.FarmId })
            .HasPrincipalKey(observation => new { observation.Id, observation.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_match_candidates_observations_same_farm");
    }
}
