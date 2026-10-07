using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class TreatmentRecommendationConfiguration
    : IEntityTypeConfiguration<TreatmentRecommendation>
{
    public void Configure(EntityTypeBuilder<TreatmentRecommendation> builder)
    {
        builder.ToTable(
            "treatment_recommendations",
            "plant",
            table =>
            {
                table.HasCheckConstraint("ck_treatment_recommendations_version_positive", "version_number >= 1");
                table.HasCheckConstraint("ck_treatment_recommendations_effective_window", "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_treatment_recommendations_supersedes_version",
                    "(version_number = 1 AND supersedes_recommendation_id IS NULL) OR (version_number > 1 AND supersedes_recommendation_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_treatment_recommendations_lifecycle",
                    "(status = 'DRAFT'::system.treatment_recommendation_status AND published_by IS NULL AND published_at IS NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'PUBLISHED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'RETIRED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NOT NULL AND retired_at IS NOT NULL AND superseded_by_recommendation_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'SUPERSEDED'::system.treatment_recommendation_status AND published_by IS NOT NULL AND published_at IS NOT NULL AND retired_by IS NULL AND retired_at IS NULL AND superseded_by_recommendation_id IS NOT NULL AND superseded_at IS NOT NULL)");
            });

        builder.HasKey(recommendation => recommendation.Id).HasName("pk_treatment_recommendations");
        builder.Property(recommendation => recommendation.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(recommendation => recommendation.Code).HasColumnName("code").HasColumnType("character varying(80)").HasMaxLength(80).IsRequired();
        builder.Property(recommendation => recommendation.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(recommendation => recommendation.PlantConditionId).HasColumnName("plant_condition_id").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.HealthLevelId).HasColumnName("health_level_id").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.Title).HasColumnName("title").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(recommendation => recommendation.Guidance).HasColumnName("guidance").HasColumnType("character varying(4000)").HasMaxLength(4000).IsRequired();
        builder.Property(recommendation => recommendation.AdvisoryDisclaimer).HasColumnName("advisory_disclaimer").HasColumnType("character varying(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(recommendation => recommendation.ExpertSource).HasColumnName("expert_source").HasColumnType("character varying(300)").HasMaxLength(300).IsRequired();
        builder.Property(recommendation => recommendation.SourceReference).HasColumnName("source_reference").HasColumnType("character varying(1000)").HasMaxLength(1000).IsRequired();
        builder.Property(recommendation => recommendation.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone");
        builder.Property(recommendation => recommendation.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(recommendation => recommendation.SupersedesRecommendationId).HasColumnName("supersedes_recommendation_id").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.Status).HasColumnName("status").HasColumnType("system.treatment_recommendation_status").HasSentinel((TreatmentRecommendationStatus)(-1)).HasDefaultValueSql("'DRAFT'::system.treatment_recommendation_status").IsRequired();
        builder.Property(recommendation => recommendation.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.PublishedBy).HasColumnName("published_by").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(recommendation => recommendation.RetiredBy).HasColumnName("retired_by").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.RetiredAt).HasColumnName("retired_at").HasColumnType("timestamp with time zone");
        builder.Property(recommendation => recommendation.SupersededByRecommendationId).HasColumnName("superseded_by_recommendation_id").HasColumnType("uuid");
        builder.Property(recommendation => recommendation.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(recommendation => recommendation.Version).IsRowVersion();
        builder.Property(recommendation => recommendation.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(recommendation => recommendation.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(recommendation => new { recommendation.Code, recommendation.VersionNumber }).HasDatabaseName("uq_treatment_recommendations_code_version").IsUnique();
        builder.HasIndex(recommendation => recommendation.Code).HasDatabaseName("uq_treatment_recommendations_one_published").HasFilter("status = 'PUBLISHED'::system.treatment_recommendation_status").IsUnique();
        builder.HasIndex(recommendation => new { recommendation.PlantConditionId, recommendation.HealthLevelId, recommendation.EffectiveFrom }).HasDatabaseName("ix_treatment_recommendations_applicability");
        builder.HasIndex(recommendation => recommendation.SupersedesRecommendationId).HasDatabaseName("uq_treatment_recommendations_supersedes").HasFilter("supersedes_recommendation_id IS NOT NULL").IsUnique();

        builder.HasOne<PlantCondition>().WithMany().HasForeignKey(recommendation => recommendation.PlantConditionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_treatment_recommendations_plant_conditions");
        builder.HasOne<HealthLevel>().WithMany().HasForeignKey(recommendation => recommendation.HealthLevelId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_treatment_recommendations_health_levels");
        builder.HasOne<TreatmentRecommendation>().WithMany().HasForeignKey(recommendation => recommendation.SupersedesRecommendationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_treatment_recommendations_supersedes");
        builder.HasOne<TreatmentRecommendation>().WithMany().HasForeignKey(recommendation => recommendation.SupersededByRecommendationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_treatment_recommendations_superseded_by");
    }
}
