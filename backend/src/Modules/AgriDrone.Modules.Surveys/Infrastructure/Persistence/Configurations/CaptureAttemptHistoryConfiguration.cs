using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CaptureAttemptHistoryConfiguration : IEntityTypeConfiguration<CaptureAttemptHistory>
{
    public void Configure(EntityTypeBuilder<CaptureAttemptHistory> builder)
    {
        builder.ToTable(
            "capture_attempt_history",
            "survey",
            table => table.HasCheckConstraint(
                "ck_capture_attempt_history_sequence",
                "transition_sequence >= 1"));

        builder.HasKey(history => history.Id).HasName("pk_capture_attempt_history");
        builder.HasAlternateKey(history => new { history.Id, history.CaptureAttemptId })
            .HasName("uq_capture_attempt_history_id_attempt");
        builder.Property(history => history.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(history => history.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(history => history.TransitionSequence).HasColumnName("transition_sequence").HasColumnType("bigint").IsRequired();
        builder.Ignore(history => history.AttemptVersion);
        builder.Property(history => history.Action).HasColumnName("action").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(history => history.FromStatus).HasColumnName("from_status").HasColumnType("system.capture_attempt_status");
        builder.Property(history => history.ToStatus).HasColumnName("to_status").HasColumnType("system.capture_attempt_status").IsRequired();
        builder.Property(history => history.ActorId).HasColumnName("actor_id").HasColumnType("uuid");
        builder.Property(history => history.Source).HasColumnName("source").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(history => history.Reason).HasColumnName("reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(history => history.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(history => history.CorrelationId).HasColumnName("correlation_id").HasColumnType("uuid").IsRequired();
        builder.Property(history => history.OperationId).HasColumnName("operation_id").HasColumnType("uuid");
        builder.Property(history => history.PayloadFingerprint).HasColumnName("payload_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128);
        builder.Property(history => history.PredecessorHistoryId).HasColumnName("predecessor_history_id").HasColumnType("uuid");
        builder.Property(history => history.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(history => new { history.CaptureAttemptId, history.TransitionSequence })
            .HasDatabaseName("uq_capture_attempt_history_sequence")
            .IsUnique();
        builder.HasIndex(history => new { history.CaptureAttemptId, history.OperationId })
            .HasDatabaseName("uq_capture_attempt_history_operation")
            .HasFilter("operation_id IS NOT NULL")
            .IsUnique();
        builder.HasIndex(history => history.CorrelationId)
            .HasDatabaseName("ix_capture_attempt_history_correlation");
        builder.HasIndex(history => history.ActorId)
            .HasDatabaseName("ix_capture_attempt_history_actor");
        builder.HasIndex(history => new { history.PredecessorHistoryId, history.CaptureAttemptId })
            .HasDatabaseName("ix_capture_attempt_history_predecessor");

        builder.HasOne<CaptureAttemptHistory>()
            .WithMany()
            .HasForeignKey(history => new { history.PredecessorHistoryId, history.CaptureAttemptId })
            .HasPrincipalKey(predecessor => new { predecessor.Id, predecessor.CaptureAttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_attempt_history_predecessor_same_attempt");
    }
}
