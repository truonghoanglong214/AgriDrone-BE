using AgriDrone.Modules.Missions.Domain.Drones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class DroneConfiguration : IEntityTypeConfiguration<Drone>
{
    public void Configure(EntityTypeBuilder<Drone> builder)
    {
        builder.ToTable(
            "drones",
            "mission",
            tableBuilder => tableBuilder.HasComment(
                "Physical drone inventory. A drone can be reused across multiple farms."));

        builder.HasKey(drone => drone.Id).HasName("pk_drones");

        builder.Property(drone => drone.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(drone => drone.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(drone => drone.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(drone => drone.Model)
            .HasColumnName("model")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(drone => drone.SerialNumber)
            .HasColumnName("serial_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(drone => drone.Status)
            .HasColumnName("status")
            .HasColumnType("system.drone_status")
            .HasDefaultValueSql("'AVAILABLE'::system.drone_status")
            .IsRequired();

        builder.Property(drone => drone.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(drone => drone.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(drone => drone.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(drone => drone.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(drone => drone.Code)
            .HasDatabaseName("uq_drones_code")
            .IsUnique();

        builder.HasIndex(drone => drone.SerialNumber)
            .HasDatabaseName("uq_drones_serial_number")
            .IsUnique();
    }
}
