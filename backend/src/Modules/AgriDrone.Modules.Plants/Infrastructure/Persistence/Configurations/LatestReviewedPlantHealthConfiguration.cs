using System.Text.Json;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class LatestReviewedPlantHealthConfiguration : IEntityTypeConfiguration<LatestReviewedPlantHealth>
{
    public void Configure(EntityTypeBuilder<LatestReviewedPlantHealth> builder)
    {
        builder.ToTable("latest_reviewed_plant_health", "plant", t => t.HasCheckConstraint("ck_latest_reviewed_health_preliminary",
            "publication_version > 0 AND result_nature = 'PRELIMINARY_SUSPECTED' AND length(btrim(team_label_limitation)) > 0 AND jsonb_typeof(findings) = 'array' AND jsonb_array_length(findings) > 0"));
        builder.HasKey(p => p.PlantId).HasName("pk_latest_reviewed_plant_health");
        builder.Property(p => p.Findings).HasColumnType("jsonb");
        builder.Property(p => p.ResultNature).HasMaxLength(40);
        builder.Property(p => p.TeamLabelLimitation).HasMaxLength(2000);
        builder.Property(p => p.Version).IsRowVersion();
        builder.HasIndex(p => new { p.TenantId, p.FarmId }).HasDatabaseName("ix_latest_reviewed_health_farm");
        builder.HasOne<Plant>().WithMany().HasForeignKey(p => new { p.PlantId, p.FarmId })
            .HasPrincipalKey(p => new { p.Id, p.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_latest_reviewed_health_plant");
        foreach (var p in builder.Metadata.GetProperties()) p.SetColumnName(p.Name == "Version" ? "xmin" : JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
    }
}
