using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyAppointmentNoShowConfiguration
    : IEntityTypeConfiguration<SurveyAppointmentNoShow>
{
    public void Configure(EntityTypeBuilder<SurveyAppointmentNoShow> builder)
    {
        builder.ToTable(
            "survey_appointment_no_shows",
            "survey",
            table => table.HasCheckConstraint(
                "ck_survey_appointment_no_shows_occurrence",
                "occurrence_index > 0"));
        builder.HasKey(value => value.Id).HasName("pk_survey_appointment_no_shows");
        builder.Property(value => value.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(value => value.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.SurveyAppointmentId).HasColumnName("survey_appointment_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.OccurrenceIndex).HasColumnName("occurrence_index").HasColumnType("integer").IsRequired();
        builder.Property(value => value.Disposition).HasColumnName("disposition").HasColumnType("character varying(40)").HasConversion<string>().IsRequired();
        builder.Property(value => value.RecordedBy).HasColumnName("recorded_by").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.Reason).HasColumnName("reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(value => value.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(value => value.OperationId).HasColumnName("operation_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.PayloadFingerprint).HasColumnName("payload_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(value => value.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(value => new { value.SurveyOrderId, value.OccurrenceIndex })
            .HasDatabaseName("uq_survey_appointment_no_shows_order_occurrence")
            .IsUnique();
        builder.HasIndex(value => value.SurveyAppointmentId)
            .HasDatabaseName("uq_survey_appointment_no_shows_appointment")
            .IsUnique();
        builder.HasIndex(value => value.OperationId)
            .HasDatabaseName("uq_survey_appointment_no_shows_operation")
            .IsUnique();

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(value => value.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_appointment_no_shows_order");
        builder.HasOne<SurveyAppointment>()
            .WithMany()
            .HasForeignKey(value => value.SurveyAppointmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_appointment_no_shows_appointment");
    }
}
