using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyAppointmentConfiguration : IEntityTypeConfiguration<SurveyAppointment>
{
    public void Configure(EntityTypeBuilder<SurveyAppointment> builder)
    {
        builder.ToTable(
            "survey_appointments",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_appointments_window", "proposed_end_at > proposed_start_at");
                table.HasCheckConstraint(
                    "ck_survey_appointments_confirmation",
                    "(status = 'CONFIRMED'::system.survey_appointment_status AND confirmed_by_tenant_owner_id IS NOT NULL AND confirmed_at IS NOT NULL) OR " +
                    "(status <> 'CONFIRMED'::system.survey_appointment_status AND confirmed_at IS NULL)");
                table.HasCheckConstraint(
                    "ck_survey_appointments_revision_chain",
                    "(revision_number = 1 AND previous_appointment_id IS NULL) OR " +
                    "(revision_number > 1 AND previous_appointment_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_survey_appointments_superseded",
                    "(status = 'SUPERSEDED'::system.survey_appointment_status AND superseded_at IS NOT NULL) OR " +
                    "(status <> 'SUPERSEDED'::system.survey_appointment_status AND superseded_at IS NULL)");
                table.HasCheckConstraint(
                    "ck_survey_appointments_reschedule_reason",
                    "status <> 'RESCHEDULE_REQUESTED'::system.survey_appointment_status OR " +
                    "(reschedule_reason IS NOT NULL AND length(btrim(reschedule_reason)) > 0)");
            });
        builder.HasKey(appointment => appointment.Id).HasName("pk_survey_appointments");
        builder.Property(appointment => appointment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(appointment => appointment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.Purpose)
            .HasColumnName("purpose")
            .HasColumnType("system.survey_appointment_purpose")
            .IsRequired();
        builder.Property(appointment => appointment.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").HasDefaultValue(1).IsRequired();
        builder.Property(appointment => appointment.PreviousAppointmentId).HasColumnName("previous_appointment_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.ProposedBySystemManagerId).HasColumnName("proposed_by_system_manager_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.ProposedStartAt).HasColumnName("proposed_start_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.ProposedEndAt).HasColumnName("proposed_end_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.Status).HasColumnName("status").HasColumnType("system.survey_appointment_status").IsRequired();
        builder.Property(appointment => appointment.ConfirmedByTenantOwnerId).HasColumnName("confirmed_by_tenant_owner_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.RescheduleReason).HasColumnName("reschedule_reason").HasColumnType("character varying(1000)").HasMaxLength(1000);
        builder.Property(appointment => appointment.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.Version).IsRowVersion();
        builder.Property(appointment => appointment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(appointment => appointment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(appointment => new { appointment.SurveyOrderId, appointment.Purpose }).HasDatabaseName("uq_survey_appointments_one_active_per_order_purpose").HasFilter("status IN ('PROPOSED'::system.survey_appointment_status, 'CONFIRMED'::system.survey_appointment_status, 'RESCHEDULE_REQUESTED'::system.survey_appointment_status)").IsUnique();
        builder.HasIndex(appointment => new { appointment.SurveyOrderId, appointment.Purpose, appointment.RevisionNumber }).HasDatabaseName("uq_survey_appointments_revision").IsUnique();
        builder.HasIndex(appointment => appointment.PreviousAppointmentId).HasDatabaseName("uq_survey_appointments_previous").HasFilter("previous_appointment_id IS NOT NULL").IsUnique();
        builder.HasIndex(appointment => new { appointment.Status, appointment.ProposedStartAt }).HasDatabaseName("ix_survey_appointments_schedule");
        builder.HasOne(appointment => appointment.SurveyOrder).WithMany(order => order.Appointments).HasForeignKey(appointment => appointment.SurveyOrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_appointments_orders_order_id");
        builder.HasOne<SurveyAppointment>().WithOne().HasForeignKey<SurveyAppointment>(appointment => appointment.PreviousAppointmentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_appointments_previous_appointment_id");
    }
}

