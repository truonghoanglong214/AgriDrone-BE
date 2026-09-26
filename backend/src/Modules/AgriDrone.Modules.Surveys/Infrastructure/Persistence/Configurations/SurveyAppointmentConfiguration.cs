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
            });
        builder.HasKey(appointment => appointment.Id).HasName("pk_survey_appointments");
        builder.Property(appointment => appointment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(appointment => appointment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.ProposedStartAt).HasColumnName("proposed_start_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.ProposedEndAt).HasColumnName("proposed_end_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.Status).HasColumnName("status").HasColumnType("system.survey_appointment_status").IsRequired();
        builder.Property(appointment => appointment.ConfirmedByTenantOwnerId).HasColumnName("confirmed_by_tenant_owner_id").HasColumnType("uuid");
        builder.Property(appointment => appointment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.RescheduleReason).HasColumnName("reschedule_reason").HasColumnType("text");
        builder.Property(appointment => appointment.Version).IsRowVersion();
        builder.Property(appointment => appointment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(appointment => appointment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(appointment => appointment.SurveyOrderId).HasDatabaseName("uq_survey_appointments_one_active_per_order").HasFilter("status IN ('PROPOSED'::system.survey_appointment_status, 'CONFIRMED'::system.survey_appointment_status, 'RESCHEDULE_REQUESTED'::system.survey_appointment_status)").IsUnique();
        builder.HasIndex(appointment => new { appointment.Status, appointment.ProposedStartAt }).HasDatabaseName("ix_survey_appointments_schedule");
        builder.HasOne(appointment => appointment.SurveyOrder).WithMany(order => order.Appointments).HasForeignKey(appointment => appointment.SurveyOrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_appointments_orders_order_id");
    }
}

