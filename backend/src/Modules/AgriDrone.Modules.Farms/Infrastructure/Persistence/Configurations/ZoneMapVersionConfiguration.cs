using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class ZoneMapVersionConfiguration : IEntityTypeConfiguration<ZoneMapVersion>
{
    public void Configure(EntityTypeBuilder<ZoneMapVersion> builder)
    {
        builder.ToTable(
            "zone_map_versions",
            "farm",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Versioned planting grid for a zone; a confirmed version is the matching baseline.");
                tableBuilder.HasCheckConstraint(
                    "ck_zone_map_versions_version_positive",
                    "version_number >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_zone_map_versions_bearing_range",
                    "grid_bearing_deg IS NULL OR " +
                    "(grid_bearing_deg >= 0 AND grid_bearing_deg < 360)");
                tableBuilder.HasCheckConstraint(
                    "ck_zone_map_versions_spacing_positive",
                    "(row_spacing_m IS NULL OR row_spacing_m > 0) AND " +
                    "(plant_spacing_m IS NULL OR plant_spacing_m > 0)");
                tableBuilder.HasCheckConstraint(
                    "ck_zone_map_versions_confirmation",
                    "((status IN ('CONFIRMED'::system.map_version_status, " +
                    "'SUPERSEDED'::system.map_version_status)) AND " +
                    "confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL) OR " +
                    "((status IN ('DRAFT'::system.map_version_status, " +
                    "'REJECTED'::system.map_version_status)) AND " +
                    "confirmed_by IS NULL AND confirmed_at IS NULL)");
            });

        builder.HasKey(mapVersion => mapVersion.Id).HasName("pk_zone_map_versions");
        builder.HasAlternateKey(mapVersion => new
        {
            mapVersion.Id,
            mapVersion.FarmId
        }).HasName("uq_zone_map_versions_id_farm");
        builder.HasAlternateKey(mapVersion => new
        {
            mapVersion.Id,
            mapVersion.ZoneId,
            mapVersion.FarmId
        }).HasName("uq_zone_map_versions_id_zone_farm");

        builder.Property(mapVersion => mapVersion.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(mapVersion => mapVersion.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(mapVersion => mapVersion.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(mapVersion => mapVersion.SourceMissionId)
            .HasColumnName("source_mission_id")
            .HasColumnType("uuid");

        builder.Property(mapVersion => mapVersion.VersionNumber)
            .HasColumnName("version_number")
            .HasColumnType("integer");

        builder.Property(mapVersion => mapVersion.Status)
            .HasColumnName("status")
            .HasColumnType("system.map_version_status")
            .HasDefaultValueSql("'DRAFT'::system.map_version_status")
            .IsRequired();

        builder.Property(mapVersion => mapVersion.GridBearingDeg)
            .HasColumnName("grid_bearing_deg")
            .HasColumnType("numeric(6,2)")
            .HasPrecision(6, 2);

        builder.Property(mapVersion => mapVersion.RowSpacingM)
            .HasColumnName("row_spacing_m")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(mapVersion => mapVersion.PlantSpacingM)
            .HasColumnName("plant_spacing_m")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(mapVersion => mapVersion.AlgorithmVersion)
            .HasColumnName("algorithm_version")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(mapVersion => mapVersion.Parameters)
            .HasColumnName("parameters")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(mapVersion => mapVersion.ConfirmedBy)
            .HasColumnName("confirmed_by")
            .HasColumnType("uuid");

        builder.Property(mapVersion => mapVersion.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(mapVersion => mapVersion.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(mapVersion => new { mapVersion.ZoneId, mapVersion.VersionNumber })
            .HasDatabaseName("uq_zone_map_versions_zone_version")
            .IsUnique();

        builder.HasIndex(mapVersion => mapVersion.ZoneId)
            .HasDatabaseName("ux_zone_map_versions_one_confirmed")
            .HasFilter("status = 'CONFIRMED'::system.map_version_status")
            .IsUnique();

        builder.HasIndex(mapVersion => mapVersion.SourceMissionId)
            .HasDatabaseName("ix_zone_map_versions_source_mission");

        builder.HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(mapVersion => new { mapVersion.ZoneId, mapVersion.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_zone_map_versions_zones_same_farm");
    }
}
