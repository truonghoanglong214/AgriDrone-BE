using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantProfileConfiguration : IEntityTypeConfiguration<PlantProfile>
{
    public void Configure(EntityTypeBuilder<PlantProfile> builder)
    {
        builder.ToTable(
            "plant_profiles",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Physical dragon-fruit pole profile tracking identity, baseline location, and lifecycle lineage.");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_profiles_status",
                    "status IN ('Active', 'NeedsReview', 'Retired', 'Replaced')");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_profiles_predecessor",
                    "(status != 'Replaced') OR (replacement_reason IS NOT NULL)");
            });

        builder.HasKey(p => p.Id).HasName("pk_plant_profiles");
        builder.HasAlternateKey(p => new { p.Id, p.FarmId }).HasName("uq_plant_profiles_id_farm");
        builder.HasAlternateKey(p => new { p.Id, p.TenantId, p.FarmId }).HasName("uq_plant_profiles_id_tenant_farm");

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.PhysicalPoleIdentifier)
            .HasColumnName("physical_pole_identifier")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.BaselineLocation)
            .HasColumnName("baseline_location")
            .HasColumnType("geometry(Point,4326)")
            .IsRequired();

        builder.Property(p => p.CurrentLocation)
            .HasColumnName("current_location")
            .HasColumnType("geometry(Point,4326)")
            .IsRequired();

        builder.Property(p => p.BaselineMapVersionId)
            .HasColumnName("baseline_map_version_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.CurrentMapVersionId)
            .HasColumnName("current_map_version_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.PredecessorProfileId)
            .HasColumnName("predecessor_profile_id")
            .HasColumnType("uuid");

        builder.Property(p => p.ReplacementReason)
            .HasColumnName("replacement_reason")
            .HasColumnType("text");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(p => new { p.FarmId, p.PhysicalPoleIdentifier })
            .HasDatabaseName("uq_plant_profiles_active_identifier")
            .HasFilter("status = 'Active'")
            .IsUnique();

        builder.HasIndex(p => new { p.FarmId, p.Status })
            .HasDatabaseName("ix_plant_profiles_farm_status");

        builder.HasIndex(p => p.CurrentLocation)
            .HasDatabaseName("ix_plant_profiles_current_location_gist")
            .HasMethod("gist");

        builder.HasIndex(p => p.PredecessorProfileId)
            .HasDatabaseName("ix_plant_profiles_predecessor");

        builder.HasOne<PlantProfile>()
            .WithMany()
            .HasForeignKey(p => p.PredecessorProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_profiles_predecessor");
    }
}
