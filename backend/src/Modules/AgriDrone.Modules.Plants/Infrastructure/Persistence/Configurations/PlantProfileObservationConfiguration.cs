using AgriDrone.Modules.Plants.Domain.Plants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantProfileObservationConfiguration : IEntityTypeConfiguration<PlantProfileObservation>
{
    public void Configure(EntityTypeBuilder<PlantProfileObservation> builder)
    {
        builder.ToTable("plant_profile_observations", "plant");

        builder.HasKey(o => o.Id).HasName("pk_plant_profile_observations");

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(o => o.PlantProfileId).HasColumnName("plant_profile_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.FarmBaseMapVersionId).HasColumnName("farm_base_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.ObservedLocation).HasColumnName("observed_location").HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(o => o.ObservedDistanceM).HasColumnName("observed_distance_m").HasColumnType("double precision").IsRequired();
        builder.Property(o => o.CandidateId).HasColumnName("candidate_id").HasColumnType("uuid");
        builder.Property(o => o.ObservedBy).HasColumnName("observed_by").HasColumnType("uuid").IsRequired();
        builder.Property(o => o.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(o => o.PlantProfileId).HasDatabaseName("ix_plant_profile_observations_profile");
        builder.HasIndex(o => new { o.FarmId, o.SurveyOrderId }).HasDatabaseName("ix_plant_profile_observations_farm_order");

        builder.HasOne<PlantProfile>()
            .WithMany()
            .HasForeignKey(o => o.PlantProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_plant_profile_observations_profile");
    }
}
