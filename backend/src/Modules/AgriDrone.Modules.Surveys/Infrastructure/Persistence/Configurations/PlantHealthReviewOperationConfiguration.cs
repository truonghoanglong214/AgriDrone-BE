using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthReviewOperationConfiguration : IEntityTypeConfiguration<PlantHealthReviewOperation>
{
    public void Configure(EntityTypeBuilder<PlantHealthReviewOperation> builder)
    {
        builder.ToTable("plant_health_review_operations", "survey");
        builder.HasKey(r => r.Id).HasName("pk_plant_health_review_operations");
        builder.HasIndex(r => new { r.TenantId, r.ActorId, r.OperationId }).IsUnique().HasDatabaseName("uq_health_review_operation");
        builder.Property(r => r.Fingerprint).HasMaxLength(64);
        builder.Property(r => r.Outcome).HasColumnType("jsonb");
        builder.HasOne<PlantHealthImportBatch>().WithMany().HasForeignKey(r => new { r.BatchId, r.TenantId, r.SurveyResultId })
            .HasPrincipalKey(r => new { r.Id, r.TenantId, r.SurveyResultId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_health_review_operation_batch");
        foreach (var p in builder.Metadata.GetProperties()) p.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
    }
}
