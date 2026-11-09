using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthPublicationConfiguration : IEntityTypeConfiguration<PlantHealthPublication>
{
    public void Configure(EntityTypeBuilder<PlantHealthPublication> builder)
    {
        builder.ToTable("plant_health_publications", "survey", t => t.HasCheckConstraint("ck_health_publication_payload",
            "publication_version > 0 AND length(approval_fingerprint) = 64 AND length(fingerprint) = 64 AND jsonb_typeof(payload) = 'object'"));
        builder.HasKey(p => p.Id).HasName("pk_plant_health_publications");
        builder.HasAlternateKey(p => new { p.Id, p.TenantId, p.FarmId, p.SurveyResultId }).HasName("uq_health_publication_context");
        builder.HasIndex(p => p.SurveyResultId).IsUnique().HasDatabaseName("uq_health_publication_result");
        builder.HasIndex(p => new { p.TenantId, p.FarmId, p.PublicationVersion }).IsUnique().HasDatabaseName("uq_health_publication_farm_version");
        builder.Property(p => p.Payload).HasColumnType("jsonb");
        builder.Property(p => p.Fingerprint).HasMaxLength(64);
        builder.Property(p => p.ApprovalFingerprint).HasMaxLength(64);
        builder.HasOne<SurveyResult>().WithMany().HasForeignKey(p => new { p.SurveyResultId, p.TenantId, p.FarmId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_publication_result");
        builder.HasOne<PlantHealthImportBatch>().WithMany().HasForeignKey(p => new { p.BatchId, p.TenantId, p.FarmId, p.SurveyResultId })
            .HasPrincipalKey(b => new { b.Id, b.TenantId, b.FarmId, b.SurveyResultId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_publication_batch");
        foreach (var p in builder.Metadata.GetProperties()) p.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
    }
}
