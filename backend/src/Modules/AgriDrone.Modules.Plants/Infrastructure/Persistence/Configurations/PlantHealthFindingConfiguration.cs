using System.Text.Json;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthFindingConfiguration : IEntityTypeConfiguration<PlantHealthFinding>
{
    public void Configure(EntityTypeBuilder<PlantHealthFinding> builder)
    {
        builder.ToTable("plant_health_findings", "plant", table =>
        {
            table.HasCheckConstraint("ck_health_finding_version", "source_version > 0 AND length(fingerprint) = 64");
            table.HasCheckConstraint("ck_health_finding_resolution", "resolution_state IN ('PENDING_REVIEW', 'NEEDS_REVIEW')");
            table.HasCheckConstraint("ck_health_finding_point", "ST_IsValid(original_position) AND NOT ST_IsEmpty(original_position) AND ST_SRID(original_position) = 4326");
        });
        builder.HasKey(f => f.Id).HasName("pk_plant_health_findings");
        builder.HasAlternateKey(f => new { f.Id, f.TenantId, f.FarmId, f.SurveyResultId }).HasName("uq_health_finding_context");
        builder.HasIndex(f => new { f.TenantId, f.MissionId, f.SourceFindingId, f.SourceVersion }).IsUnique().HasDatabaseName("uq_health_finding_source");
        builder.HasIndex(f => new { f.SurveyResultId, f.ResolutionState }).HasDatabaseName("ix_health_finding_review_queue");
        builder.Property(f => f.OriginalPosition).HasColumnType("geometry(Point,4326)");
        builder.Property(f => f.Original).HasColumnType("jsonb");
        builder.Property(f => f.SuspectedClassCode).HasMaxLength(80);
        builder.Property(f => f.ResolutionState).HasMaxLength(40);
        builder.Property(f => f.BoundaryClassification).HasMaxLength(40);
        builder.Property(f => f.Fingerprint).HasMaxLength(64);
        builder.Property(f => f.Version).IsRowVersion();
        builder.HasOne<Plant>().WithMany().HasForeignKey(f => new { f.PlantId, f.FarmId })
            .HasPrincipalKey(p => new { p.Id, p.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_finding_plant");
        builder.HasOne<PlantHealthFinding>().WithMany().HasForeignKey(f => new { f.SupersedesFindingId, f.TenantId, f.FarmId, f.SurveyResultId })
            .HasPrincipalKey(f => new { f.Id, f.TenantId, f.FarmId, f.SurveyResultId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_finding_supersedes");
        foreach (var property in builder.Metadata.GetProperties())
            property.SetColumnName(property.Name == "Version" ? "xmin" : JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

public sealed class PlantHealthBatchFindingConfiguration : IEntityTypeConfiguration<PlantHealthBatchFinding>
{
    public void Configure(EntityTypeBuilder<PlantHealthBatchFinding> builder)
    {
        builder.ToTable("plant_health_batch_findings", "plant");
        builder.HasKey(f => new { f.BatchId, f.FindingId }).HasName("pk_plant_health_batch_findings");
        builder.HasOne<PlantHealthFinding>().WithMany().HasForeignKey(f => new { f.FindingId, f.TenantId, f.FarmId, f.SurveyResultId })
            .HasPrincipalKey(f => new { f.Id, f.TenantId, f.FarmId, f.SurveyResultId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_batch_finding_context");
        foreach (var property in builder.Metadata.GetProperties())
            property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
