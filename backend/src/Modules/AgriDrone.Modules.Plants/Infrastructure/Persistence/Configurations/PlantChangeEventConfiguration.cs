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
            tableBuilder => tableBuilder.HasComment(
                "Reviewable mapping differences such as missing, new, removed, or dead plants."));

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

        builder.Property(changeEvent => changeEvent.ObservedLocation)
            .HasColumnName("observed_location")
            .HasColumnType("geometry(Point,4326)");

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

        builder.HasOne<Plant>()
            .WithMany()
            .HasForeignKey(changeEvent => new { changeEvent.PlantId, changeEvent.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_change_event_plant_same_farm");
    }
}
