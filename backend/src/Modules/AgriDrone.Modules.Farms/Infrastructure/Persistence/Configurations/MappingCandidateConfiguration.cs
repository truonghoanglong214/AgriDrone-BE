using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class MappingCandidateConfiguration : IEntityTypeConfiguration<MappingCandidate>
{
    public void Configure(EntityTypeBuilder<MappingCandidate> builder)
    {
        builder.ToTable("mapping_candidates", "farm");

        builder.HasKey(c => c.Id).HasName("pk_mapping_candidates");

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.BatchId)
            .HasColumnName("batch_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(c => c.CandidateId)
            .HasColumnName("candidate_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.SourceObservationId)
            .HasColumnName("source_observation_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.EvidenceMediaAssetIds)
            .HasColumnName("evidence_media_asset_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                json => string.IsNullOrWhiteSpace(json)
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null) ?? new List<Guid>())
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<Guid>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(c => c.OriginalPosition)
            .HasColumnName("original_position")
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.Property(c => c.ProposedRowIndex)
            .HasColumnName("proposed_row_index")
            .HasColumnType("integer");

        builder.Property(c => c.ProposedColumnIndex)
            .HasColumnName("proposed_column_index")
            .HasColumnType("integer");

        builder.Property(c => c.PositionConfidence)
            .HasColumnName("position_confidence")
            .HasColumnType("numeric(5,4)")
            .IsRequired();

        builder.Property(c => c.BoundaryClassification)
            .HasColumnName("boundary_classification")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.MeasuredBoundaryDistanceM)
            .HasColumnName("measured_boundary_distance_m")
            .HasColumnType("numeric(8,3)")
            .IsRequired();

        builder.Property(c => c.BoundaryThresholdM)
            .HasColumnName("boundary_threshold_m")
            .HasColumnType("numeric(8,3)")
            .IsRequired();

        builder.Property(c => c.ProposedMatchedPlantId)
            .HasColumnName("proposed_matched_plant_id")
            .HasColumnType("uuid");

        builder.Property(c => c.MatchConfidence)
            .HasColumnName("match_confidence")
            .HasColumnType("numeric(5,4)");

        builder.Property(c => c.TreeId)
            .HasColumnName("tree_id")
            .HasMaxLength(100);

        builder.Property(c => c.ReviewDecision)
            .HasColumnName("review_decision")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CorrectedPosition)
            .HasColumnName("corrected_position")
            .HasColumnType("geometry(Point, 4326)");

        builder.Property(c => c.CorrectedRowIndex)
            .HasColumnName("corrected_row_index")
            .HasColumnType("integer");

        builder.Property(c => c.CorrectedColumnIndex)
            .HasColumnName("corrected_column_index")
            .HasColumnType("integer");

        builder.Property(c => c.CorrectedMatchedPlantId)
            .HasColumnName("corrected_matched_plant_id")
            .HasColumnType("uuid");

        builder.Property(c => c.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");

        builder.Property(c => c.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(c => c.ReviewReason)
            .HasColumnName("review_reason")
            .HasMaxLength(1000);

        builder.Property(c => c.Version)
            .IsRowVersion();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(c => new { c.BatchId, c.CandidateId })
            .HasDatabaseName("uq_mapping_candidates_batch_candidate")
            .IsUnique();

        builder.HasIndex(c => new { c.BatchId, c.SourceObservationId })
            .HasDatabaseName("uq_mapping_candidates_batch_observation")
            .IsUnique();

        builder.HasIndex(c => c.OriginalPosition)
            .HasMethod("gist")
            .HasDatabaseName("ix_mapping_candidates_original_position_gist");
    }
}
