using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyRequestReviewConfiguration : IEntityTypeConfiguration<SurveyRequestReview>
{
    public void Configure(EntityTypeBuilder<SurveyRequestReview> builder)
    {
        builder.ToTable("survey_request_reviews", "survey", table => table.HasCheckConstraint("ck_survey_request_reviews_reason", "length(btrim(reason)) > 0"));
        builder.HasKey(review => review.Id).HasName("pk_survey_request_reviews");
        builder.Property(review => review.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(review => review.SurveyRequestId).HasColumnName("survey_request_id").HasColumnType("uuid");
        builder.Property(review => review.Decision).HasColumnName("decision").HasColumnType("system.survey_review_decision").IsRequired();
        builder.Property(review => review.ChecklistSnapshot).HasColumnName("checklist_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(review => review.Reason).HasColumnName("reason").HasColumnType("text").IsRequired();
        builder.Property(review => review.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(review => review.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(review => new { review.SurveyRequestId, review.ReviewedAt }).HasDatabaseName("ix_survey_request_reviews_timeline");
        builder.HasOne(review => review.SurveyRequest).WithMany(request => request.Reviews).HasForeignKey(review => review.SurveyRequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_request_reviews_requests_request_id");
    }
}

