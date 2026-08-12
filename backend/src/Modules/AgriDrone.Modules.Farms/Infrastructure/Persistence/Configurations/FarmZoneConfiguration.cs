using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmZoneConfiguration : IEntityTypeConfiguration<FarmZone>
{
    public void Configure(EntityTypeBuilder<FarmZone> builder)
    {
        builder.ToTable(
            "farm_zones",
            "farm",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Management zones inside a farm, optionally represented by polygons on the map.");
                tableBuilder.HasCheckConstraint(
                    "ck_farm_zones_area_nonnegative",
                    "area_hectares IS NULL OR area_hectares >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_farm_zones_boundary_valid",
                    "boundary IS NULL OR ST_IsValid(boundary)");
            });

        builder.HasKey(zone => zone.Id).HasName("pk_farm_zones");
        builder.HasAlternateKey(zone => new { zone.Id, zone.FarmId })
            .HasName("uq_farm_zones_id_farm");

        builder.Property(zone => zone.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(zone => zone.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(zone => zone.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(zone => zone.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(zone => zone.Boundary)
            .HasColumnName("boundary")
            .HasColumnType("geometry(Polygon,4326)");

        builder.Property(zone => zone.AreaHectares)
            .HasColumnName("area_hectares")
            .HasColumnType("numeric(12,4)")
            .HasPrecision(12, 4);

        builder.Property(zone => zone.Status)
            .HasColumnName("status")
            .HasColumnType("system.general_status")
            .HasDefaultValueSql("'ACTIVE'::system.general_status")
            .IsRequired();

        builder.Property(zone => zone.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(zone => zone.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(zone => zone.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(zone => zone.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(zone => new { zone.FarmId, zone.Code })
            .HasDatabaseName("ux_farm_zones_farm_code_active")
            .HasFilter("deleted_at IS NULL")
            .IsUnique();

        builder.HasIndex(zone => zone.FarmId)
            .HasDatabaseName("ix_farm_zones_farm");

        builder.HasIndex(zone => zone.Boundary)
            .HasDatabaseName("ix_farm_zones_boundary_gist")
            .HasMethod("gist");

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(zone => zone.FarmId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_farm_zones_farms_farm_id");
    }
}
