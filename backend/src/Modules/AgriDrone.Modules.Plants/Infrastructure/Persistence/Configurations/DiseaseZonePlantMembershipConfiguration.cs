using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class DiseaseZonePlantMembershipConfiguration
    : IEntityTypeConfiguration<DiseaseZonePlantMembership>
{
    public void Configure(EntityTypeBuilder<DiseaseZonePlantMembership> builder)
    {
        builder.ToTable(
            "disease_zone_memberships",
            "plant",
            table =>
            {
                table.HasCheckConstraint("ck_disease_zone_memberships_version_positive", "membership_version >= 1");
                table.HasCheckConstraint("ck_disease_zone_memberships_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            });

        builder.HasKey(membership => membership.Id).HasName("pk_disease_zone_memberships");
        builder.Property(membership => membership.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(membership => membership.DiseaseZoneId).HasColumnName("disease_zone_id").HasColumnType("uuid");
        builder.Property(membership => membership.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(membership => membership.PlantId).HasColumnName("plant_id").HasColumnType("uuid");
        builder.Property(membership => membership.MembershipVersion).HasColumnName("membership_version").HasColumnType("integer");
        builder.Property(membership => membership.Kind).HasColumnName("kind").HasColumnType("system.disease_zone_membership_kind").IsRequired();
        builder.Property(membership => membership.Confidence).HasColumnName("confidence").HasColumnType("numeric(5,4)").HasPrecision(5, 4);
        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(membership => new { membership.DiseaseZoneId, membership.MembershipVersion, membership.PlantId }).HasDatabaseName("uq_disease_zone_memberships_snapshot_plant").IsUnique();
        builder.HasIndex(membership => new { membership.PlantId, membership.DiseaseZoneId }).HasDatabaseName("ix_disease_zone_memberships_plant_zone");
        builder.HasOne<Plant>().WithMany().HasForeignKey(membership => new { membership.PlantId, membership.FarmId }).HasPrincipalKey(plant => new { plant.Id, plant.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zone_memberships_plants_same_farm");
    }
}
