using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("payment_events", "survey");
        builder.HasKey(paymentEvent => paymentEvent.Id).HasName("pk_payment_events");
        builder.Property(paymentEvent => paymentEvent.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(paymentEvent => paymentEvent.SurveyPaymentId).HasColumnName("survey_payment_id").HasColumnType("uuid");
        builder.Property(paymentEvent => paymentEvent.Provider).HasColumnName("provider").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(paymentEvent => paymentEvent.DeduplicationKey).HasColumnName("deduplication_key").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(paymentEvent => paymentEvent.EventType).HasColumnName("event_type").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(paymentEvent => paymentEvent.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(paymentEvent => paymentEvent.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        builder.Property(paymentEvent => paymentEvent.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(paymentEvent => new { paymentEvent.Provider, paymentEvent.DeduplicationKey }).HasDatabaseName("uq_payment_events_provider_dedup").IsUnique();
        builder.HasIndex(paymentEvent => new { paymentEvent.SurveyPaymentId, paymentEvent.OccurredAt }).HasDatabaseName("ix_payment_events_timeline");
        builder.HasOne(paymentEvent => paymentEvent.SurveyPayment).WithMany(payment => payment.Events).HasForeignKey(paymentEvent => paymentEvent.SurveyPaymentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payment_events_payments_payment_id");
    }
}

