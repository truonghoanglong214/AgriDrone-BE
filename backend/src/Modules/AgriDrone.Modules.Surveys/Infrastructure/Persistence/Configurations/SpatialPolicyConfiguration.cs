using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SpatialPolicyConfiguration : IEntityTypeConfiguration<SpatialPolicy>
{
    public void Configure(EntityTypeBuilder<SpatialPolicy> builder)
    {
        builder.ToTable(
            "spatial_policies",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_spatial_policies_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_spatial_policies_tolerances",
                    "boundary_tolerance_m > 0 AND matching_distance_threshold_m > 0 AND row_bearing_tolerance_deg > 0 AND grid_spacing_tolerance_m > 0");
                table.HasCheckConstraint(
                    "ck_spatial_policies_version",
                    "version_number >= 1");
            });

        builder.HasKey(policy => policy.Id).HasName("pk_spatial_policies");
        builder.Property(policy => policy.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(policy => policy.StreamKey).HasColumnName("stream_key").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(policy => policy.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
        builder.Property(policy => policy.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(policy => policy.BoundaryToleranceM).HasColumnName("boundary_tolerance_m").HasColumnType("double precision").IsRequired();
        builder.Property(policy => policy.MatchingDistanceThresholdM).HasColumnName("matching_distance_threshold_m").HasColumnType("double precision").IsRequired();
        builder.Property(policy => policy.RowBearingToleranceDeg).HasColumnName("row_bearing_tolerance_deg").HasColumnType("double precision").IsRequired();
        builder.Property(policy => policy.GridSpacingToleranceM).HasColumnName("grid_spacing_tolerance_m").HasColumnType("double precision").IsRequired();
        builder.Property(policy => policy.PolicyFingerprint).HasColumnName("policy_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(policy => policy.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(policy => policy.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(policy => policy.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(policy => policy.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(policy => new { policy.StreamKey, policy.VersionNumber })
            .HasDatabaseName("uq_spatial_policies_stream_version")
            .IsUnique();

        builder.HasIndex(policy => policy.EffectiveFrom)
            .HasDatabaseName("ix_spatial_policies_effective")
            .IsDescending(true);

        builder.HasOne<SpatialPolicy>()
            .WithMany()
            .HasForeignKey(policy => policy.PredecessorVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_spatial_policies_predecessor");
    }
}
