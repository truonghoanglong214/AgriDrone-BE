using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyResultReviewOperationConfiguration
    : IEntityTypeConfiguration<SurveyResultReviewOperation>
{
    public void Configure(EntityTypeBuilder<SurveyResultReviewOperation> builder)
    {
        builder.ToTable(
            "survey_result_review_operations",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_result_review_operations_fingerprint",
                    "request_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_result_review_operations_action",
                    "action IN ('RecordComboReview', 'FreezeInternalResult')");
            });

        builder.HasKey(r => r.Id).HasName("pk_survey_result_review_operations");

        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ActorId).HasColumnName("actor_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.Action).HasColumnName("action").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.ApiVersion).HasColumnName("api_version").HasColumnType("character varying(16)").HasMaxLength(16).IsRequired();
        builder.Property(r => r.OperationId).HasColumnName("operation_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.RequestFingerprint).HasColumnName("request_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.ComboBatchId).HasColumnName("combo_batch_id").HasColumnType("uuid");
        builder.Property(r => r.ReviewRevisionId).HasColumnName("review_revision_id").HasColumnType("uuid");
        builder.Property(r => r.ApprovedResultRevisionId).HasColumnName("approved_result_revision_id").HasColumnType("uuid");
        builder.Property(r => r.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid");
        builder.Property(r => r.PoleCountSnapshotId).HasColumnName("pole_count_snapshot_id").HasColumnType("uuid");
        builder.Property(r => r.Outcome).HasColumnName("outcome").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.ActorId, r.Action, r.ApiVersion, r.OperationId })
            .HasDatabaseName("uq_survey_result_review_operations_actor_action_op")
            .IsUnique();

        builder.HasIndex(r => new { r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasDatabaseName("ix_survey_result_review_operations_order");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(r => new { r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(o => new { o.Id, o.TenantId, o.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_result_review_operations_order");
    }
}
