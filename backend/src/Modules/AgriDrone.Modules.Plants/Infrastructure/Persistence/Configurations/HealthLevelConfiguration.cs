using AgriDrone.Modules.Plants.Domain.Conditions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class HealthLevelConfiguration : IEntityTypeConfiguration<HealthLevel>
{
    public void Configure(EntityTypeBuilder<HealthLevel> builder)
    {
        builder.ToTable(
            "health_levels",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Global ordered health/severity levels shared by plants, scans and condition detections.");
                tableBuilder.HasCheckConstraint(
                    "ck_health_levels_semantics",
                    "(code = 'UNKNOWN' AND rank IS NULL AND is_healthy = FALSE) OR " +
                    "(code = 'HEALTHY' AND rank = 0 AND is_healthy = TRUE) OR " +
                    "(code NOT IN ('UNKNOWN', 'HEALTHY') AND rank > 0 AND is_healthy = FALSE)");
            });

        builder.HasKey(level => level.Id).HasName("pk_health_levels");

        builder.Property(level => level.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(level => level.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(level => level.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(level => level.Rank)
            .HasColumnName("rank")
            .HasColumnType("integer");

        builder.Property(level => level.IsHealthy)
            .HasColumnName("is_healthy")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(level => level.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(level => level.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(level => level.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(level => level.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(level => level.Code)
            .HasDatabaseName("uq_health_levels_code")
            .IsUnique();

        builder.HasIndex(level => level.Rank)
            .HasDatabaseName("uq_health_levels_rank")
            .HasFilter("rank IS NOT NULL")
            .IsUnique();
    }
}
