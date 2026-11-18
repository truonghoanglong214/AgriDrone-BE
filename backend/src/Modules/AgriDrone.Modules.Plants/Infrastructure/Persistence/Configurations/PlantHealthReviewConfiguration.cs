using System.Text.Json;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthReviewEntryConfiguration : IEntityTypeConfiguration<PlantHealthReviewEntry>
{
    public void Configure(EntityTypeBuilder<PlantHealthReviewEntry> builder)
    {
        builder.ToTable("plant_health_reviews", "plant", t =>
        {
            t.HasCheckConstraint("ck_health_review_subject", "(kind = 'Finding' AND finding_id = subject_id AND finding_id IS NOT NULL AND zone_id IS NULL AND boundary_exception_id IS NULL) OR (kind IN ('Zone','Guidance') AND zone_id = subject_id AND zone_id IS NOT NULL AND finding_id IS NULL AND boundary_exception_id IS NULL) OR (kind = 'Boundary' AND boundary_exception_id = subject_id AND boundary_exception_id IS NOT NULL AND finding_id IS NULL AND zone_id IS NULL)");
            t.HasCheckConstraint("ck_health_review_decision", "(kind = 'Finding' AND decision IN ('Approve','Correct','Reject','NonDisease','Ambiguous')) OR (kind = 'Zone' AND decision IN ('Approve','Correct','Reject','Ambiguous')) OR (kind = 'Boundary' AND decision IN ('AcceptCorrectedPosition','ExcludeEvidence','NeedsEvidence')) OR (kind = 'Guidance' AND decision IN ('Select','RejectAll','NeedsEvidence'))");
        });
        builder.HasKey(r => r.Id).HasName("pk_plant_health_reviews");
        builder.HasAlternateKey(r => new { r.Id, r.BatchId, r.Kind, r.SubjectId, r.TenantId, r.FarmId, r.SurveyResultId }).HasName("uq_health_review_context");
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Decision).HasConversion<string>().HasMaxLength(40);
        builder.Property(r => r.Reason).HasMaxLength(2000);
        builder.Property(r => r.Snapshot).HasColumnType("jsonb");
        builder.HasIndex(r => new { r.BatchId, r.Kind, r.SubjectId, r.ReviewedAt, r.Id }).HasDatabaseName("ix_health_review_history");
        builder.HasOne<PlantHealthReviewEntry>().WithMany().HasForeignKey(r => new { r.PreviousReviewId, r.BatchId, r.Kind, r.SubjectId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(r => new { r.Id, r.BatchId, r.Kind, r.SubjectId, r.TenantId, r.FarmId, r.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_previous");
        builder.HasOne<PlantHealthBatchFinding>().WithMany().HasForeignKey(r => new { r.BatchId, r.FindingId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_batch_finding");
        builder.HasOne<PlantHealthFinding>().WithMany().HasForeignKey(r => new { r.FindingId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.FarmId, r.SurveyResultId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_finding_context");
        builder.HasOne<DiseaseZone>().WithMany().HasForeignKey(r => new { r.ZoneId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(z => new { z.Id, z.TenantId, z.FarmId, z.SurveyResultId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_zone_context");
        builder.HasOne<PlantHealthBoundaryException>().WithMany().HasForeignKey(r => new { r.BoundaryExceptionId, r.BatchId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(e => new { e.Id, e.BatchId, e.TenantId, e.FarmId, e.SurveyResultId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_boundary_context");
        foreach (var p in builder.Metadata.GetProperties()) p.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
    }
}

public sealed class PlantHealthReviewStateConfiguration : IEntityTypeConfiguration<PlantHealthReviewState>
{
    public void Configure(EntityTypeBuilder<PlantHealthReviewState> builder)
    {
        builder.ToTable("plant_health_review_states", "plant");
        builder.HasKey(r => new { r.BatchId, r.Kind, r.SubjectId }).HasName("pk_plant_health_review_states");
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.Version).IsRowVersion();
        builder.HasOne<PlantHealthReviewEntry>().WithMany().HasForeignKey(r => new { r.CurrentReviewId, r.BatchId, r.Kind, r.SubjectId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(r => new { r.Id, r.BatchId, r.Kind, r.SubjectId, r.TenantId, r.FarmId, r.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_state_current");
        foreach (var p in builder.Metadata.GetProperties()) p.SetColumnName(p.Name == "Version" ? "xmin" : JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
    }
}
