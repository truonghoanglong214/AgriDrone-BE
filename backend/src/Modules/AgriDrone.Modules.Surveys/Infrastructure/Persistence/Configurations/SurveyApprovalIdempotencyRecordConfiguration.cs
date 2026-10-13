using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyApprovalIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<SurveyApprovalIdempotencyRecord>
{
    public void Configure(
        EntityTypeBuilder<SurveyApprovalIdempotencyRecord> builder)
    {
        builder.ToTable(
            "survey_approval_idempotency",
            "survey",
            table => table.HasCheckConstraint(
                "ck_survey_approval_idempotency_completion",
                "(result_snapshot IS NULL AND completed_at IS NULL) OR " +
                "(result_snapshot IS NOT NULL AND completed_at IS NOT NULL)"));

        builder.HasKey(record => record.Id)
            .HasName("pk_survey_approval_idempotency");
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");
        builder.Property(record => record.SurveyRequestId)
            .HasColumnName("survey_request_id")
            .HasColumnType("uuid");
        builder.Property(record => record.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid");
        builder.Property(record => record.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasColumnType("character varying(100)")
            .HasMaxLength(SurveyApprovalIdempotencyRecord.MaximumKeyLength)
            .IsRequired();
        builder.Property(record => record.CommandFingerprint)
            .HasColumnName("command_fingerprint")
            .HasColumnType("character(64)")
            .HasMaxLength(
                SurveyApprovalIdempotencyRecord.CommandFingerprintLength)
            .IsFixedLength()
            .IsRequired();
        builder.Property(record => record.ResultSnapshot)
            .HasColumnName("result_snapshot")
            .HasColumnType("jsonb");
        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(record => record.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(record => record.SurveyRequestId)
            .HasDatabaseName(
                "uq_survey_approval_idempotency_request")
            .IsUnique();
        builder.HasIndex(record => new
            {
                record.ActorId,
                record.IdempotencyKey
            })
            .HasDatabaseName(
                "uq_survey_approval_idempotency_actor_key")
            .IsUnique();
        builder.HasOne<SurveyRequest>()
            .WithOne()
            .HasForeignKey<SurveyApprovalIdempotencyRecord>(record =>
                record.SurveyRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_survey_approval_idempotency_requests_request_id");
    }
}
