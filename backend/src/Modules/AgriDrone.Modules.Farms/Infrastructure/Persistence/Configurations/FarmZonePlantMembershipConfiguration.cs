using AgriDrone.Modules.Farms.Domain.Zones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmZonePlantMembershipConfiguration
    : IEntityTypeConfiguration<FarmZonePlantMembership>
{
    public void Configure(EntityTypeBuilder<FarmZonePlantMembership> builder)
    {
        builder.ToTable("farm_zone_plant_memberships", "farm");
        builder.HasKey(membership => membership.Id)
            .HasName("pk_farm_zone_plant_memberships");
        builder.Property(membership => membership.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(membership => membership.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(membership => membership.ZoneId).HasColumnName("zone_id").HasColumnType("uuid").IsRequired();
        builder.Property(membership => membership.PlantId).HasColumnName("plant_id").HasColumnType("uuid").IsRequired();
        builder.Property(membership => membership.Source)
            .HasColumnName("source")
            .HasConversion(
                value => value == FarmZonePlantMembershipSource.AutoGeometry
                    ? "AUTO_GEOMETRY"
                    : "MANUAL_OVERRIDE",
                value => value == "AUTO_GEOMETRY"
                    ? FarmZonePlantMembershipSource.AutoGeometry
                    : FarmZonePlantMembershipSource.ManualOverride)
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(membership => membership.IsAssigned).HasColumnName("is_assigned").HasDefaultValue(true).IsRequired();
        builder.Property(membership => membership.AssignedBy).HasColumnName("assigned_by").HasColumnType("uuid").IsRequired();
        builder.Property(membership => membership.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(membership => membership.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(membership => membership.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(membership => membership.Version).IsRowVersion();

        builder.HasIndex(membership => membership.PlantId)
            .HasDatabaseName("uq_farm_zone_plant_memberships_one_active_per_plant")
            .HasFilter("effective_to IS NULL")
            .IsUnique();
        builder.HasIndex(membership => new { membership.ZoneId, membership.PlantId })
            .HasDatabaseName("ix_farm_zone_plant_memberships_zone_plant");
        builder.HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(membership => new { membership.ZoneId, membership.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_zone_plant_memberships_zone_same_farm");
    }
}
