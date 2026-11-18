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
                    "(status IN ('DRAFT'::system.farm_base_map_status, 'INTERNAL_APPROVED'::system.farm_base_map_status) AND published_by IS NULL AND published_at IS NULL) OR " +
                    "(status IN ('PUBLISHED'::system.farm_base_map_status, 'SUPERSEDED'::system.farm_base_map_status) AND published_by IS NOT NULL AND published_at IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_farm_base_map_versions_baseline_lineage",
                    "(is_baseline = TRUE AND version_number = 1 AND predecessor_version_id IS NULL AND baseline_map_version_id = id) OR " +
                    "(is_baseline = FALSE AND version_number > 1 AND predecessor_version_id IS NOT NULL AND baseline_map_version_id IS NOT NULL AND baseline_map_version_id <> id)");
            });
        builder.HasKey(map => map.Id).HasName("pk_farm_base_map_versions");
        builder.HasAlternateKey(map => new { map.Id, map.FarmId }).HasName("uq_farm_base_map_versions_id_farm");
        builder.HasAlternateKey(map => new { map.Id, map.TenantId, map.FarmId }).HasName("uq_farm_base_map_versions_id_tenant_farm");
        builder.Property(map => map.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(map => map.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(map => map.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(map => map.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(map => map.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid");
        builder.Property(map => map.IsBaseline).HasColumnName("is_baseline").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(map => map.BaselineMapVersionId).HasColumnName("baseline_map_version_id").HasColumnType("uuid");
        builder.Property(map => map.SourceSurveyOrderId).HasColumnName("source_survey_order_id").HasColumnType("uuid");
        builder.Property(map => map.SourceMissionGroupId).HasColumnName("source_mission_group_id").HasColumnType("uuid");
        builder.Property(map => map.SourceMissionId).HasColumnName("source_mission_id").HasColumnType("uuid");
        builder.Property(map => map.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(map => map.HandoffId).HasColumnName("handoff_id").HasColumnType("uuid");
        builder.Property(map => map.GridBearingDeg).HasColumnName("grid_bearing_deg").HasColumnType("double precision").IsRequired();
        builder.Property(map => map.RowSpacingM).HasColumnName("row_spacing_m").HasColumnType("double precision").IsRequired();
        builder.Property(map => map.PlantSpacingM).HasColumnName("plant_spacing_m").HasColumnType("double precision").IsRequired();
        builder.Property(map => map.Status)
            .HasColumnName("status")
            .HasColumnType("system.farm_base_map_status")
            .HasSentinel((FarmBaseMapStatus)(-1))
            .HasDefaultValueSql("'DRAFT'::system.farm_base_map_status")
            .IsRequired();
        builder.Property(map => map.PublishedBy).HasColumnName("published_by").HasColumnType("uuid");
        builder.Property(map => map.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(map => map.ApprovedInternalBy).HasColumnName("approved_internal_by").HasColumnType("uuid");
        builder.Property(map => map.ApprovedInternalAt).HasColumnName("approved_internal_at").HasColumnType("timestamp with time zone");
        builder.Property(map => map.Version).IsRowVersion();
        builder.Property(map => map.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(map => map.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(map => new { map.TenantId, map.FarmId, map.VersionNumber }).HasDatabaseName("uq_farm_base_map_versions_tenant_farm_version").IsUnique();
        builder.HasIndex(map => new { map.TenantId, map.FarmId }).HasDatabaseName("uq_farm_base_map_versions_one_baseline").HasFilter("is_baseline = TRUE").IsUnique();
        builder.HasIndex(map => new { map.TenantId, map.FarmId }).HasDatabaseName("uq_farm_base_map_versions_one_draft").HasFilter("status = 'DRAFT'::system.farm_base_map_status").IsUnique();
        builder.HasIndex(map => new { map.TenantId, map.FarmId }).HasDatabaseName("uq_farm_base_map_versions_one_published").HasFilter("status = 'PUBLISHED'::system.farm_base_map_status").IsUnique();
        builder.HasIndex(map => map.SourceSurveyOrderId).HasDatabaseName("uq_farm_base_map_versions_source_order").IsUnique();
        builder.HasOne<Farm>().WithMany().HasForeignKey(map => new { map.FarmId, map.TenantId }).HasPrincipalKey(farm => new { farm.Id, farm.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_base_map_versions_farms_same_tenant");
        builder.HasOne<FarmBaseMapVersion>().WithMany().HasForeignKey(map => map.PredecessorVersionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_base_map_versions_predecessor");
        builder.HasOne<FarmBaseMapVersion>().WithMany().HasForeignKey(map => map.BaselineMapVersionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_base_map_versions_baseline");
    }
}
