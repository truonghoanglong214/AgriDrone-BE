using AgriDrone.Modules.Missions.Domain.Drones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class DroneMaintenanceRecordConfiguration :
    IEntityTypeConfiguration<DroneMaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<DroneMaintenanceRecord> builder)
    {
        builder.ToTable("drone_maintenance_records", "mission");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.DroneId).HasColumnName("drone_id");
        builder.Property(record => record.StartedAt).HasColumnName("started_at");
        builder.Property(record => record.StartedBy).HasColumnName("started_by");
        builder.Property(record => record.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(record => record.ClosedAt).HasColumnName("closed_at");
        builder.Property(record => record.ClosedBy).HasColumnName("closed_by");
        builder.Property(record => record.ClosingStatus)
            .HasColumnName("closing_status")
            .HasColumnType("system.drone_status");
        builder.Property(record => record.NextMaintenanceAt)
            .HasColumnName("next_maintenance_at");
        builder.HasOne<Drone>().WithMany().HasForeignKey(record => record.DroneId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(record => record.DroneId)
            .HasDatabaseName("uq_drone_maintenance_open")
            .HasFilter("closed_at IS NULL").IsUnique();
        builder.HasIndex(record => new { record.DroneId, record.StartedAt })
            .HasDatabaseName("ix_drone_maintenance_history");
    }
}
