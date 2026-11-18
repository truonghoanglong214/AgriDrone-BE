using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthImportBatchConfiguration : IEntityTypeConfiguration<PlantHealthImportBatch>
{
    public void Configure(EntityTypeBuilder<PlantHealthImportBatch> builder)
    {
        builder.ToTable("plant_health_import_batches", "survey", table =>
        {
            table.HasCheckConstraint("ck_health_import_source_version", "source_version > 0 AND blocking_count >= 0");
            table.HasCheckConstraint("ck_health_import_fingerprint", "length(fingerprint) = 64");
        });
        builder.HasKey(b => b.Id).HasName("pk_plant_health_import_batches");
        builder.HasAlternateKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId }).HasName("uq_health_import_context");
        builder.HasAlternateKey(b => new { b.Id, b.TenantId, b.SurveyResultId }).HasName("uq_health_import_result_tenant");
        builder.HasIndex(b => new { b.TenantId, b.HandoffId }).IsUnique().HasDatabaseName("uq_health_import_handoff");
        builder.HasIndex(b => new { b.TenantId, b.MissionId, b.JobId }).IsUnique().HasDatabaseName("uq_health_import_job");
        builder.HasIndex(b => new { b.SurveyResultId, b.SourceVersion }).HasDatabaseName("ix_health_import_result_version");
        builder.Property(b => b.Fingerprint).HasMaxLength(64);
        builder.Property(b => b.OriginalPayload).HasColumnType("jsonb");
        builder.Property(b => b.Version).IsRowVersion();
        builder.HasOne<SurveyResult>().WithMany().HasForeignKey(b => new { b.SurveyResultId, b.SurveyOrderId, b.FarmId })
            .HasPrincipalKey(r => new { r.Id, r.SurveyOrderId, r.FarmId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_import_result_context");
        builder.HasOne<SurveyResult>().WithMany().HasForeignKey(b => new { b.SurveyResultId, b.TenantId, b.FarmId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.FarmId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_import_result_tenant");
        builder.HasOne<PlantHealthImportBatch>().WithMany().HasForeignKey(b => new { b.SupersedesBatchId, b.TenantId, b.FarmId, b.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_health_import_supersedes");
        foreach (var property in builder.Metadata.GetProperties())
            property.SetColumnName(property.Name == "Version" ? "xmin" : JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
