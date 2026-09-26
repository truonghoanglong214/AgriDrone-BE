using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class HarvestReadinessAssessmentConfiguration : IEntityTypeConfiguration<HarvestReadinessAssessment>
{
    public void Configure(EntityTypeBuilder<HarvestReadinessAssessment> builder)
    {
        builder.ToTable(
            "harvest_readiness_assessments",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_harvest_readiness_confidence", "ai_confidence IS NULL OR ai_confidence BETWEEN 0 AND 1");
                table.HasCheckConstraint("ck_harvest_readiness_review", "(status = 'PENDING'::system.harvest_readiness_review_status AND reviewed_by IS NULL AND reviewed_at IS NULL) OR (status <> 'PENDING'::system.harvest_readiness_review_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)");
            });
        builder.HasKey(assessment => assessment.Id).HasName("pk_harvest_readiness_assessments");
        builder.Property(assessment => assessment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(assessment => assessment.SurveyResultId).HasColumnName("survey_result_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.PlantId).HasColumnName("plant_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.MissionId).HasColumnName("mission_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.AssessmentGranularity).HasColumnName("assessment_granularity").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(assessment => assessment.CriteriaVersion).HasColumnName("criteria_version").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(assessment => assessment.VisibleIndicators).HasColumnName("visible_indicators").HasColumnType("jsonb").IsRequired();
        builder.Property(assessment => assessment.AiAssessment).HasColumnName("ai_assessment").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(assessment => assessment.AiConfidence).HasColumnName("ai_confidence").HasColumnType("numeric(5,4)").HasPrecision(5, 4);
        builder.Property(assessment => assessment.CorrectedAssessment).HasColumnName("corrected_assessment").HasColumnType("character varying(100)").HasMaxLength(100);
        builder.Property(assessment => assessment.Evidence).HasColumnName("evidence").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(assessment => assessment.Status)
            .HasColumnName("status")
            .HasColumnType("system.harvest_readiness_review_status")
            .HasSentinel((HarvestReadinessReviewStatus)(-1))
            .HasDefaultValueSql("'PENDING'::system.harvest_readiness_review_status")
            .IsRequired();
        builder.Property(assessment => assessment.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(assessment => assessment.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.Property(assessment => assessment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(assessment => new { assessment.SurveyResultId, assessment.PlantId }).HasDatabaseName("ix_harvest_readiness_result_plant");
        builder.HasIndex(assessment => new { assessment.FarmId, assessment.PlantId, assessment.CreatedAt }).HasDatabaseName("ix_harvest_readiness_profile_timeline").IsDescending(false, false, true);
        builder.HasOne(assessment => assessment.SurveyResult).WithMany(result => result.HarvestReadinessAssessments).HasForeignKey(assessment => new { assessment.SurveyResultId, assessment.SurveyOrderId, assessment.FarmId }).HasPrincipalKey(result => new { result.Id, result.SurveyOrderId, result.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_harvest_readiness_results_same_order_farm");
    }
}

