using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class MappingCandidateZoneConfiguration : IEntityTypeConfiguration<MappingCandidateZone>
{
    public void Configure(EntityTypeBuilder<MappingCandidateZone> builder)
    {
        builder.ToTable("mapping_candidate_zones", "farm");

        builder.HasKey(z => z.Id).HasName("pk_mapping_candidate_zones");

        builder.Property(z => z.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(z => z.BatchId)
            .HasColumnName("batch_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(z => z.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(z => z.ExpectedCurrentZoneMapVersionId)
            .HasColumnName("expected_current_zone_map_version_id")
            .HasColumnType("uuid");

        builder.Property(z => z.GridBearingDeg)
            .HasColumnName("grid_bearing_deg")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(z => z.RowSpacingM)
            .HasColumnName("row_spacing_m")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(z => z.PlantSpacingM)
            .HasColumnName("plant_spacing_m")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(z => z.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(z => new { z.BatchId, z.ZoneId })
            .HasDatabaseName("uq_mapping_candidate_zones_batch_zone")
            .IsUnique();

        builder.HasMany(z => z.Candidates)
            .WithOne(c => c.Zone)
            .HasForeignKey(c => new { c.BatchId, c.ZoneId })
            .HasPrincipalKey(z => new { z.BatchId, z.ZoneId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_mapping_candidates_candidate_zone");
    }
}
