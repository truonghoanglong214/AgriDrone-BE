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
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Tenant-owned physical drone inventory reusable across farms in the same tenant.");
                tableBuilder.HasCheckConstraint(
                    "ck_drones_registration_dates",
                    "registration_expiry_date IS NULL OR registration_date IS NULL OR " +
                    "registration_expiry_date >= registration_date");
                tableBuilder.HasCheckConstraint(
                    "ck_drones_weight_positive",
                    "weight_kg IS NULL OR weight_kg > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_drones_maintenance_dates",
                    "next_maintenance_at IS NULL OR last_maintenance_at IS NULL OR " +
                    "next_maintenance_at >= last_maintenance_at");
            });

        builder.HasKey(drone => drone.Id).HasName("pk_drones");
        builder.HasAlternateKey(drone => new { drone.Id, drone.TenantId })
            .HasName("uq_drones_id_tenant");

        builder.Property(drone => drone.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(drone => drone.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

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

        builder.Property(drone => drone.Manufacturer)
            .HasColumnName("manufacturer")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(drone => drone.Specifications)
            .HasColumnName("specifications")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(drone => drone.SerialNumber)
            .HasColumnName("serial_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(drone => drone.RegistrationNumber)
            .HasColumnName("registration_number")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(drone => drone.RegistrationDate)
            .HasColumnName("registration_date")
            .HasColumnType("date");

        builder.Property(drone => drone.RegistrationExpiryDate)
            .HasColumnName("registration_expiry_date")
            .HasColumnType("date");

        builder.Property(drone => drone.WeightKg)
            .HasColumnName("weight_kg")
            .HasColumnType("numeric(8,3)")
            .HasPrecision(8, 3);

        builder.Property(drone => drone.Status)
            .HasColumnName("status")
            .HasColumnType("system.drone_status")
            .HasSentinel((DroneStatus)(-1))
            .HasDefaultValueSql("'AVAILABLE'::system.drone_status")
            .IsRequired();

        builder.Property(drone => drone.LastMaintenanceAt)
            .HasColumnName("last_maintenance_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(drone => drone.NextMaintenanceAt)
            .HasColumnName("next_maintenance_at")
            .HasColumnType("timestamp with time zone");

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

        builder.HasIndex(drone => new { drone.TenantId, drone.Code })
            .HasDatabaseName("uq_drones_tenant_code")
            .IsUnique();

        builder.HasIndex(drone => new { drone.TenantId, drone.SerialNumber })
            .HasDatabaseName("uq_drones_tenant_serial_number")
            .HasFilter("serial_number IS NOT NULL")
            .IsUnique();

        builder.HasIndex(drone => new { drone.TenantId, drone.RegistrationNumber })
            .HasDatabaseName("uq_drones_tenant_registration_number")
            .HasFilter("registration_number IS NOT NULL")
            .IsUnique();

        builder.HasIndex(drone => drone.TenantId)
            .HasDatabaseName("ix_drones_tenant");
    }
}
