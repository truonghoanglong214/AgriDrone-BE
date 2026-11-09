using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal static class PlantHealthRelationships
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LatestReviewedPlantHealth>().HasOne<PlantHealthPublication>().WithMany()
            .HasForeignKey(p => new { p.PublicationId, p.TenantId, p.FarmId, p.SurveyResultId })
            .HasPrincipalKey(p => new { p.Id, p.TenantId, p.FarmId, p.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_latest_reviewed_health_publication");
        modelBuilder.Entity<PlantHealthReviewEntry>().HasOne<PlantHealthImportBatch>().WithMany()
            .HasForeignKey(r => new { r.BatchId, r.TenantId, r.FarmId, r.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_import_context");
        modelBuilder.Entity<PlantHealthBatchFinding>().HasOne<PlantHealthImportBatch>().WithMany()
            .HasForeignKey(f => new { f.BatchId, f.TenantId, f.FarmId, f.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_batch_finding_batch");
        modelBuilder.Entity<PlantHealthBoundaryException>().HasOne<PlantHealthImportBatch>().WithMany()
            .HasForeignKey(e => new { e.BatchId, e.TenantId, e.FarmId, e.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_boundary_exception_batch");
        modelBuilder.Entity<DiseaseZone>().HasOne<PlantHealthImportBatch>().WithMany()
            .HasForeignKey(z => new { z.ImportBatchId, z.TenantId, z.FarmId, z.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_zone_batch");
        modelBuilder.Entity<PlantHealthFinding>().HasOne<SurveyResult>().WithMany()
            .HasForeignKey(f => new { f.SurveyResultId, f.TenantId, f.FarmId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.FarmId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_finding_result");
    }
}
