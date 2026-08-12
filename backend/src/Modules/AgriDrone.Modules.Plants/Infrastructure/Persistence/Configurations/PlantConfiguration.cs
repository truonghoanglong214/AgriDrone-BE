using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantConfiguration : IEntityTypeConfiguration<Plant>
{
    public void Configure(EntityTypeBuilder<Plant> builder)
    {
        builder.ToTable(
            "plants",
            "plant",
            tableBuilder => tableBuilder.HasComment(
                "Digital Plant Profile root: one row represents one real dragon-fruit pole throughout its lifecycle."));

        builder.HasKey(plant => plant.Id).HasName("pk_plants");
        builder.HasAlternateKey(plant => new { plant.Id, plant.FarmId })
            .HasName("uq_plants_id_farm");

        builder.Property(plant => plant.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(plant => plant.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(plant => plant.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(plant => plant.PlantCode)
            .HasColumnName("plant_code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(plant => plant.Location)
            .HasColumnName("location")
            .HasColumnType("geometry(Point,4326)");

        builder.Property(plant => plant.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasColumnType("system.plant_lifecycle_status")
            .HasDefaultValueSql("'ACTIVE'::system.plant_lifecycle_status")
            .IsRequired();

        builder.Property(plant => plant.CurrentHealthStatus)
            .HasColumnName("current_health_status")
            .HasColumnType("system.health_status")
            .HasDefaultValueSql("'UNKNOWN'::system.health_status")
            .IsRequired();

        builder.Property(plant => plant.LastInspectedAt)
            .HasColumnName("last_inspected_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(plant => plant.MappedAt)
            .HasColumnName("mapped_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(plant => plant.CreatedFromMissionId)
            .HasColumnName("created_from_mission_id")
            .HasColumnType("uuid");

        builder.Property(plant => plant.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(plant => plant.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(plant => plant.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(plant => new { plant.FarmId, plant.PlantCode })
            .HasDatabaseName("uq_plants_farm_code")
            .IsUnique();

        builder.HasIndex(plant => plant.FarmId)
            .HasDatabaseName("ix_plants_farm");

        builder.HasIndex(plant => plant.ZoneId)
            .HasDatabaseName("ix_plants_zone");

        builder.HasIndex(plant => new { plant.ZoneId, plant.CurrentHealthStatus })
            .HasDatabaseName("ix_plants_zone_health");

        builder.HasIndex(plant => new { plant.FarmId, plant.LifecycleStatus })
            .HasDatabaseName("ix_plants_farm_lifecycle");

        builder.HasIndex(plant => plant.Location)
            .HasDatabaseName("ix_plants_location_gist")
            .HasMethod("gist");
    }
}
