using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmBaselinePointerConfiguration : IEntityTypeConfiguration<FarmBaselinePointer>
{
    public void Configure(EntityTypeBuilder<FarmBaselinePointer> builder)
    {
        builder.ToTable("farm_baseline_pointers", "farm");

        builder.HasKey(p => p.FarmId).HasName("pk_farm_baseline_pointers");
        builder.HasAlternateKey(p => new { p.FarmId, p.TenantId }).HasName("uq_farm_baseline_pointers_farm_tenant");

        builder.Property(p => p.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(p => p.CurrentBaselineMapVersionId).HasColumnName("current_baseline_map_version_id").HasColumnType("uuid");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(p => new { p.FarmId, p.TenantId })
            .HasPrincipalKey(f => new { f.Id, f.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_baseline_pointers_farms");

        builder.HasOne<FarmBaseMapVersion>()
            .WithMany()
            .HasForeignKey(p => p.CurrentBaselineMapVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_baseline_pointers_map_version");
    }
}
