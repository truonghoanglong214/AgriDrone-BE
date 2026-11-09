using System.Text.Json;
using AgriDrone.Modules.Plants.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantHealthBoundaryExceptionConfiguration : IEntityTypeConfiguration<PlantHealthBoundaryException>
{
    public void Configure(EntityTypeBuilder<PlantHealthBoundaryException> builder)
    {
        builder.ToTable("plant_health_boundary_exceptions", "plant");
        builder.HasKey(e => e.Id).HasName("pk_plant_health_boundary_exceptions");
        builder.HasAlternateKey(e => new { e.Id, e.BatchId, e.TenantId, e.FarmId, e.SurveyResultId }).HasName("uq_health_boundary_review_context");
        builder.HasIndex(e => new { e.BatchId, e.SourceReferenceId }).IsUnique().HasDatabaseName("uq_health_boundary_exception_source");
        builder.Property(e => e.OriginalPosition).HasColumnType("geometry(Point,4326)");
        builder.Property(e => e.Original).HasColumnType("jsonb");
        builder.Property(e => e.SourceReferenceId).HasMaxLength(200);
        builder.Property(e => e.Status).HasMaxLength(40);
        builder.Property(e => e.Version).IsRowVersion();
        foreach (var property in builder.Metadata.GetProperties())
            property.SetColumnName(property.Name == "Version" ? "xmin" : JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}
