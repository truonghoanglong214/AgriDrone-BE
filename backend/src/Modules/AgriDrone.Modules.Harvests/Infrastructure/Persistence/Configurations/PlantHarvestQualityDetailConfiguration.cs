using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.Modules.Harvests.Domain.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence.Configurations;

public sealed class PlantHarvestQualityDetailConfiguration
    : IEntityTypeConfiguration<PlantHarvestQualityDetail>
{
    public void Configure(EntityTypeBuilder<PlantHarvestQualityDetail> builder)
    {
        builder.ToTable(
            "plant_harvest_quality_details",
            "harvest",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Breakdown of one plant harvest record by configurable quality grade.");
                tableBuilder.HasCheckConstraint(
                    "ck_quality_detail_fruit_count",
                    "fruit_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_quality_detail_weight",
                    "weight_kg IS NULL OR weight_kg >= 0");
            });

        builder.HasKey(detail => new
        {
            detail.PlantHarvestRecordId,
            detail.QualityGradeId
        })
            .HasName("pk_plant_harvest_quality_details");

        builder.Property(detail => detail.PlantHarvestRecordId)
            .HasColumnName("plant_harvest_record_id")
            .HasColumnType("uuid");

        builder.Property(detail => detail.QualityGradeId)
            .HasColumnName("quality_grade_id")
            .HasColumnType("uuid");

        builder.Property(detail => detail.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(detail => detail.FruitCount)
            .HasColumnName("fruit_count")
            .HasColumnType("integer");

        builder.Property(detail => detail.WeightKg)
            .HasColumnName("weight_kg")
            .HasColumnType("numeric(10,3)")
            .HasPrecision(10, 3);

        builder.Property(detail => detail.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne(detail => detail.PlantHarvestRecord)
            .WithMany(record => record.QualityDetails)
            .HasForeignKey(detail => new
            {
                detail.PlantHarvestRecordId,
                detail.FarmId
            })
            .HasPrincipalKey(record => new { record.Id, record.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_quality_detail_record_same_farm");

        builder.HasOne(detail => detail.QualityGrade)
            .WithMany(grade => grade.PlantHarvestQualityDetails)
            .HasForeignKey(detail => detail.QualityGradeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_quality_detail_grade_global");
    }
}
