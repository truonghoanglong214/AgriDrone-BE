using AgriDrone.Modules.Plants.Domain.Conditions;
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
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Digital Plant Profile root: one row represents one real dragon-fruit pole throughout its lifecycle.");
                tableBuilder.HasCheckConstraint(
                    "ck_plants_grid_position_complete",
                    "(current_map_version_id IS NULL AND row_index IS NULL AND column_index IS NULL) OR " +
                    "(current_map_version_id IS NOT NULL AND zone_id IS NOT NULL AND " +
                    "row_index IS NOT NULL AND column_index IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_plants_grid_indices_positive",
                    "(row_index IS NULL OR row_index >= 1) AND " +
                    "(column_index IS NULL OR column_index >= 1)");
                tableBuilder.HasCheckConstraint(
                    "ck_plants_location_accuracy_nonnegative",
                    "location_accuracy_m IS NULL OR location_accuracy_m >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_plants_position_confidence",
                    "position_confidence IS NULL OR position_confidence BETWEEN 0 AND 1");
            });

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

        builder.Property(plant => plant.CurrentMapVersionId)
            .HasColumnName("current_map_version_id")
            .HasColumnType("uuid");

        builder.Property(plant => plant.RowIndex)
            .HasColumnName("row_index")
            .HasColumnType("integer");

        builder.Property(plant => plant.ColumnIndex)
            .HasColumnName("column_index")
            .HasColumnType("integer");

        builder.Property(plant => plant.LocationAccuracyM)
            .HasColumnName("location_accuracy_m")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(plant => plant.PositionConfidence)
            .HasColumnName("position_confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(plant => plant.PositionSource)
            .HasColumnName("position_source")
            .HasColumnType("system.position_source");

        builder.Property(plant => plant.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasColumnType("system.plant_lifecycle_status")
            .HasSentinel((PlantLifecycleStatus)(-1))
            .HasDefaultValueSql("'ACTIVE'::system.plant_lifecycle_status")
            .IsRequired();

        builder.Property(plant => plant.CurrentHealthLevelId)
            .HasColumnName("current_health_level_id")
            .HasColumnType("uuid")
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

        builder.HasIndex(plant => plant.CurrentMapVersionId)
            .HasDatabaseName("ix_plants_current_map_version");

        builder.HasIndex(plant => new
        {
            plant.ZoneId,
            plant.RowIndex,
            plant.ColumnIndex
        })
            .HasDatabaseName("ux_plants_active_zone_grid_position")
            .HasFilter(
                "row_index IS NOT NULL AND column_index IS NOT NULL AND " +
                "lifecycle_status IN ('ACTIVE'::system.plant_lifecycle_status, " +
                "'MISSING'::system.plant_lifecycle_status)")
            .IsUnique();

        builder.HasIndex(plant => new { plant.ZoneId, plant.CurrentHealthLevelId })
            .HasDatabaseName("ix_plants_zone_health_level");

        builder.HasIndex(plant => new { plant.FarmId, plant.LifecycleStatus })
            .HasDatabaseName("ix_plants_farm_lifecycle");

        builder.HasIndex(plant => plant.Location)
            .HasDatabaseName("ix_plants_location_gist")
            .HasMethod("gist");

        builder.HasOne(plant => plant.CurrentHealthLevel)
            .WithMany(level => level.CurrentPlants)
            .HasForeignKey(plant => plant.CurrentHealthLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_health_levels_current_health_level_id");
    }
}
