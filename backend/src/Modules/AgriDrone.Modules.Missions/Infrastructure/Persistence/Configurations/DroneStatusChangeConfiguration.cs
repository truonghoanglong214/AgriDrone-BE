using AgriDrone.Modules.Missions.Domain.Drones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure
    .Persistence.Configurations;

public sealed class DroneStatusChangeConfiguration
    : IEntityTypeConfiguration<DroneStatusChange>
{
    public void Configure(
        EntityTypeBuilder<DroneStatusChange> builder)
    {
        builder.ToTable(
            "drone_status_changes",
            "mission");

        builder.HasKey(change => change.Id)
            .HasName("pk_drone_status_changes");

        builder.Property(change => change.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(change => change.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(change => change.DroneId)
            .HasColumnName("drone_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(change => change.PreviousStatus)
            .HasColumnName("previous_status")
            .HasColumnType("system.drone_status");

        builder.Property(change => change.NewStatus)
            .HasColumnName("new_status")
            .HasColumnType("system.drone_status")
            .IsRequired();

        builder.Property(change => change.ChangedBy)
            .HasColumnName("changed_by")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(change => change.ChangedAt)
            .HasColumnName("changed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Drone>()
            .WithMany()
            .HasForeignKey(change => new
            {
                change.DroneId,
                change.TenantId
            })
            .HasPrincipalKey(drone => new
            {
                drone.Id,
                drone.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_drone_status_changes_drone_same_tenant");

        builder.HasIndex(change => new
        {
            change.TenantId,
            change.DroneId,
            change.ChangedAt
        })
            .HasDatabaseName(
                "ix_drone_status_changes_drone_changed_at");
    }
}