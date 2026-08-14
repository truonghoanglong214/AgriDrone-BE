using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantChangeEventConfiguration : IEntityTypeConfiguration<PlantChangeEvent>
{
    public void Configure(EntityTypeBuilder<PlantChangeEvent> builder)
    {
        builder.ToTable(
            "plant_change_events",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Reviewable AI/manual plant register, retire, relocate and lifecycle changes with before/after state.");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_change_source_actor",
                    "(source = 'MISSION_AI'::system.plant_change_source AND mission_id IS NOT NULL) OR " +
                    "(source = 'MANUAL'::system.plant_change_source AND created_by IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_change_has_difference",
                    "old_location IS DISTINCT FROM new_location OR " +
                    "old_row_index IS DISTINCT FROM new_row_index OR " +
                    "old_column_index IS DISTINCT FROM new_column_index OR " +
                    "old_lifecycle_status IS DISTINCT FROM new_lifecycle_status");
            });

        builder.HasKey(changeEvent => changeEvent.Id).HasName("pk_plant_change_events");

        builder.Property(changeEvent => changeEvent.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(changeEvent => changeEvent.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(changeEvent => changeEvent.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(changeEvent => changeEvent.PlantId)
            .HasColumnName("plant_id")
            .HasColumnType("uuid");

        builder.Property(changeEvent => changeEvent.ChangeType)
            .HasColumnName("change_type")
            .HasColumnType("system.plant_change_type")
            .IsRequired();

        builder.Property(changeEvent => changeEvent.Source)
            .HasColumnName("source")
            .HasColumnType("system.plant_change_source")
            .HasDefaultValueSql("'MISSION_AI'::system.plant_change_source")
            .IsRequired();

        builder.Property(changeEvent => changeEvent.OldLocation)
            .HasColumnName("old_location")
            .HasColumnType("geometry(Point,4326)");

        builder.Property(changeEvent => changeEvent.NewLocation)
            .HasColumnName("new_location")
            .HasColumnType("geometry(Point,4326)");

        builder.Property(changeEvent => changeEvent.OldRowIndex).HasColumnName("old_row_index").HasColumnType("integer");
        builder.Property(changeEvent => changeEvent.NewRowIndex).HasColumnName("new_row_index").HasColumnType("integer");
        builder.Property(changeEvent => changeEvent.OldColumnIndex).HasColumnName("old_column_index").HasColumnType("integer");
        builder.Property(changeEvent => changeEvent.NewColumnIndex).HasColumnName("new_column_index").HasColumnType("integer");

        builder.Property(changeEvent => changeEvent.OldLifecycleStatus)
            .HasColumnName("old_lifecycle_status")
            .HasColumnType("system.plant_lifecycle_status");

        builder.Property(changeEvent => changeEvent.NewLifecycleStatus)
            .HasColumnName("new_lifecycle_status")
            .HasColumnType("system.plant_lifecycle_status");

        builder.Property(changeEvent => changeEvent.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(changeEvent => changeEvent.Status)
            .HasColumnName("status")
            .HasColumnType("system.review_status")
            .HasDefaultValueSql("'PENDING'::system.review_status")
            .IsRequired();

        builder.Property(changeEvent => changeEvent.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(changeEvent => changeEvent.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");

        builder.Property(changeEvent => changeEvent.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(changeEvent => changeEvent.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(changeEvent => new { changeEvent.MissionId, changeEvent.Status })
            .HasDatabaseName("ix_plant_change_events_mission");

        builder.HasIndex(changeEvent => changeEvent.PlantId)
            .HasDatabaseName("ix_plant_change_events_plant");

        builder.HasIndex(changeEvent => new { changeEvent.FarmId, changeEvent.CreatedAt })
            .HasDatabaseName("ix_plant_change_events_farm_created")
            .IsDescending(false, true);

        builder.HasOne<Plant>()
            .WithMany()
            .HasForeignKey(changeEvent => new { changeEvent.PlantId, changeEvent.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_change_event_plant_same_farm");
    }
}
