using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyResultConfiguration : IEntityTypeConfiguration<SurveyResult>
{
    public void Configure(EntityTypeBuilder<SurveyResult> builder)
    {
        builder.ToTable(
            "survey_results",
            "survey",
            table => table.HasCheckConstraint(
                "ck_survey_results_review_publication",
                "(status = 'PENDING_REVIEW'::system.survey_result_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND published_by IS NULL AND published_at IS NULL) OR " +
                "(status = 'APPROVED'::system.survey_result_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NULL AND published_at IS NULL) OR " +
                "(status = 'PUBLISHED'::system.survey_result_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL)"));
        builder.HasKey(result => result.Id).HasName("pk_survey_results");
        builder.HasAlternateKey(result => new { result.Id, result.SurveyOrderId, result.FarmId }).HasName("uq_survey_results_id_order_farm");
        builder.HasAlternateKey(result => new { result.Id, result.FarmId }).HasName("uq_survey_results_id_farm");
        builder.Property(result => result.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(result => result.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(result => result.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(result => result.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(result => result.ServiceType).HasColumnName("service_type").HasColumnType("system.survey_service_type").IsRequired();
        builder.Property(result => result.Status)
            .HasColumnName("status")
            .HasColumnType("system.survey_result_status")
            .HasSentinel((SurveyResultStatus)(-1))
            .HasDefaultValueSql("'PENDING_REVIEW'::system.survey_result_status")
            .IsRequired();
        builder.Property(result => result.Provenance).HasColumnName("provenance").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(result => result.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(result => result.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.Property(result => result.PublishedBy).HasColumnName("published_by").HasColumnType("uuid");
        builder.Property(result => result.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(result => result.Version).IsRowVersion();
        builder.Property(result => result.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(result => result.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(result => result.SurveyOrderId).HasDatabaseName("uq_survey_results_order").IsUnique();
        builder.HasIndex(result => new { result.TenantId, result.FarmId, result.Status }).HasDatabaseName("ix_survey_results_farm_status");
        builder.HasIndex(result => new { result.FarmId, result.ServiceType, result.PublishedAt }).HasDatabaseName("ix_survey_results_profile_timeline").IsDescending(false, false, true);
        builder.HasOne(result => result.SurveyOrder).WithOne().HasForeignKey<SurveyResult>(result => new { result.SurveyOrderId, result.TenantId, result.FarmId }).HasPrincipalKey<SurveyOrder>(order => new { order.Id, order.TenantId, order.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_results_orders_same_tenant_farm");
    }
}

