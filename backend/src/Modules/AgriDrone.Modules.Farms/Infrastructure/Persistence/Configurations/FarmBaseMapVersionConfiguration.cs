using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmBaseMapVersionConfiguration : IEntityTypeConfiguration<FarmBaseMapVersion>
{
    public void Configure(EntityTypeBuilder<FarmBaseMapVersion> builder)
    {
        builder.ToTable(
            "farm_base_map_versions",
            "farm",
            table =>
            {
                table.HasCheckConstraint("ck_farm_base_map_versions_version_positive", "version_number >= 1");
                table.HasCheckConstraint(
                    "ck_farm_base_map_versions_publication",
                    "(status = 'DRAFT'::system.farm_base_map_status AND published_by IS NULL AND published_at IS NULL) OR " +
                    "(status IN ('PUBLISHED'::system.farm_base_map_status, 'SUPERSEDED'::system.farm_base_map_status) AND published_by IS NOT NULL AND published_at IS NOT NULL)");
            });
        builder.HasKey(map => map.Id).HasName("pk_farm_base_map_versions");
        builder.HasAlternateKey(map => new { map.Id, map.FarmId }).HasName("uq_farm_base_map_versions_id_farm");
        builder.HasAlternateKey(map => new { map.Id, map.TenantId, map.FarmId }).HasName("uq_farm_base_map_versions_id_tenant_farm");
        builder.Property(map => map.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(map => map.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(map => map.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(map => map.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(map => map.SourceSurveyOrderId).HasColumnName("source_survey_order_id").HasColumnType("uuid");
        builder.Property(map => map.SourceMissionGroupId).HasColumnName("source_mission_group_id").HasColumnType("uuid");
        builder.Property(map => map.Status)
            .HasColumnName("status")
            .HasColumnType("system.farm_base_map_status")
            .HasSentinel((FarmBaseMapStatus)(-1))
            .HasDefaultValueSql("'DRAFT'::system.farm_base_map_status")
            .IsRequired();
        builder.Property(map => map.PublishedBy).HasColumnName("published_by").HasColumnType("uuid");
        builder.Property(map => map.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(map => map.Version).IsRowVersion();
        builder.Property(map => map.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(map => map.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(map => new { map.FarmId, map.VersionNumber }).HasDatabaseName("uq_farm_base_map_versions_farm_version").IsUnique();
        builder.HasIndex(map => map.FarmId).HasDatabaseName("uq_farm_base_map_versions_one_published").HasFilter("status = 'PUBLISHED'::system.farm_base_map_status").IsUnique();
        builder.HasIndex(map => map.SourceSurveyOrderId).HasDatabaseName("uq_farm_base_map_versions_source_order").IsUnique();
        builder.HasOne<Farm>().WithMany().HasForeignKey(map => new { map.FarmId, map.TenantId }).HasPrincipalKey(farm => new { farm.Id, farm.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_base_map_versions_farms_same_tenant");
    }
}
