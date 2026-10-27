using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class MappingCandidateBatchConfiguration : IEntityTypeConfiguration<MappingCandidateBatch>
{
    public void Configure(EntityTypeBuilder<MappingCandidateBatch> builder)
    {
        builder.ToTable("mapping_candidate_batches", "farm");

        builder.HasKey(b => b.Id).HasName("pk_mapping_candidate_batches");

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(b => b.HandoffId)
            .HasColumnName("handoff_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.SurveyOrderId)
            .HasColumnName("survey_order_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.FarmBoundaryVersionId)
            .HasColumnName("farm_boundary_version_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.FarmBaseMapVersionId)
            .HasColumnName("farm_base_map_version_id")
            .HasColumnType("uuid");

        builder.Property(b => b.ExpectedCurrentFarmBaseMapVersionId)
            .HasColumnName("expected_current_farm_base_map_version_id")
            .HasColumnType("uuid");

        builder.Property(b => b.JobId)
            .HasColumnName("job_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.ModelVersionId)
            .HasColumnName("model_version_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(b => b.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.AlgorithmVersion)
            .HasColumnName("algorithm_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.BoundaryPolicyVersion)
            .HasColumnName("boundary_policy_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.ProducedAt)
            .HasColumnName("produced_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");

        builder.Property(b => b.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(b => b.ReviewReason)
            .HasColumnName("review_reason")
            .HasMaxLength(1000);

        builder.Property(b => b.Version)
            .IsRowVersion();

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(b => b.HandoffId)
            .HasDatabaseName("uq_mapping_candidate_batches_handoff")
            .IsUnique();

        builder.HasIndex(b => new { b.FarmId, b.SurveyOrderId })
            .HasDatabaseName("ix_mapping_candidate_batches_farm_order");

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(b => new { b.FarmId, b.TenantId })
            .HasPrincipalKey(f => new { f.Id, f.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mapping_candidate_batches_farms");

        builder.HasMany(b => b.Zones)
            .WithOne(z => z.Batch)
            .HasForeignKey(z => z.BatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mapping_candidate_zones_batch");

        builder.HasMany(b => b.Candidates)
            .WithOne(c => c.Batch)
            .HasForeignKey(c => c.BatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mapping_candidates_batch");
    }
}
