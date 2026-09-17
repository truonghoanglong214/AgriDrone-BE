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

        builder.Property(grade => grade.RevisionNumber)
            .HasColumnName("revision_number")
            .HasColumnType("integer")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(grade => grade.SupersedesId)
            .HasColumnName("supersedes_id")
            .HasColumnType("uuid");

        builder.Property(grade => grade.Version)
            .HasColumnName("version")
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(grade => grade.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");

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

        builder.HasIndex(grade => new
            {
                grade.Code,
                grade.RevisionNumber
            })
            .HasDatabaseName("uq_quality_grades_code_revision")
            .IsUnique();

        builder.HasIndex(grade => grade.Code)
            .HasDatabaseName("uq_quality_grades_active_code")
            .HasFilter("is_active = TRUE")
            .IsUnique();

        builder.HasIndex(grade => grade.SupersedesId)
            .HasDatabaseName("uq_quality_grades_supersedes")
            .HasFilter("supersedes_id IS NOT NULL")
            .IsUnique();

        builder.HasOne<HarvestQualityGrade>()
            .WithMany()
            .HasForeignKey(grade => grade.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_quality_grades_superseded_grade_id");
    }
}
