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
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criterion_snapshot",
                    "(harvest_readiness_criterion_id IS NULL AND criteria_code IS NULL AND criteria_version_number IS NULL AND criteria_status_snapshot IS NULL) OR " +
                    "(harvest_readiness_criterion_id IS NOT NULL AND criteria_code IS NOT NULL AND criteria_version_number IS NOT NULL AND criteria_status_snapshot IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_harvest_readiness_ai_provenance",
                    "ai_threshold_profile_id IS NULL OR ai_model_version_id IS NOT NULL");
            });
        builder.HasKey(assessment => assessment.Id).HasName("pk_harvest_readiness_assessments");
        builder.Property(assessment => assessment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(assessment => assessment.SurveyResultId).HasColumnName("survey_result_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.PlantId).HasColumnName("plant_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.MissionId).HasColumnName("mission_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.HarvestReadinessCriterionId).HasColumnName("harvest_readiness_criterion_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.CriteriaCode).HasColumnName("criteria_code").HasColumnType("character varying(80)").HasMaxLength(80);
        builder.Property(assessment => assessment.CriteriaVersionNumber).HasColumnName("criteria_version_number").HasColumnType("integer");
        builder.Property(assessment => assessment.CriteriaStatusSnapshot).HasColumnName("criteria_status_snapshot").HasColumnType("character varying(30)").HasMaxLength(30);
        builder.Property(assessment => assessment.AiModelVersionId).HasColumnName("ai_model_version_id").HasColumnType("uuid");
        builder.Property(assessment => assessment.AiThresholdProfileId).HasColumnName("ai_threshold_profile_id").HasColumnType("uuid");
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
        builder.HasIndex(assessment => new { assessment.HarvestReadinessCriterionId, assessment.CriteriaVersionNumber }).HasDatabaseName("ix_harvest_readiness_criterion_snapshot");
        builder.HasIndex(assessment => assessment.AiModelVersionId)
            .HasDatabaseName("ix_harvest_readiness_ai_model_version");
        builder.HasIndex(assessment => new
        {
            assessment.AiThresholdProfileId,
            assessment.AiModelVersionId
        })
            .HasDatabaseName("ix_harvest_readiness_ai_threshold_model");
        builder.HasOne(assessment => assessment.SurveyResult).WithMany(result => result.HarvestReadinessAssessments).HasForeignKey(assessment => new { assessment.SurveyResultId, assessment.SurveyOrderId, assessment.FarmId }).HasPrincipalKey(result => new { result.Id, result.SurveyOrderId, result.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_harvest_readiness_results_same_order_farm");
        builder.HasOne(assessment => assessment.Criterion)
            .WithMany()
            .HasForeignKey(assessment => new
            {
                assessment.HarvestReadinessCriterionId,
                assessment.CriteriaVersionNumber
            })
            .HasPrincipalKey(criterion => new
            {
                criterion.Id,
                criterion.VersionNumber
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_readiness_assessments_criterion_version");
    }
}

