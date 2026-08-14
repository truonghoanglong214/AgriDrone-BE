using AgriDrone.Modules.Harvests.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence.Configurations;

public sealed class HarvestQualityGradeConfiguration
    : IEntityTypeConfiguration<HarvestQualityGrade>
{
    public void Configure(EntityTypeBuilder<HarvestQualityGrade> builder)
    {
        builder.ToTable(
            "harvest_quality_grades",
            "harvest",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Global System Admin-managed quality grades such as A/B/C/Rejected.");
                tableBuilder.HasCheckConstraint(
                    "ck_quality_display_order",
                    "display_order >= 0");
            });

        builder.HasKey(grade => grade.Id).HasName("pk_harvest_quality_grades");

        builder.Property(grade => grade.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(grade => grade.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(grade => grade.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(grade => grade.DisplayOrder)
            .HasColumnName("display_order")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(grade => grade.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(grade => grade.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(grade => grade.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(grade => grade.Code)
            .HasDatabaseName("uq_quality_grades_code")
            .IsUnique();
    }
}
