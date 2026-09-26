using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyPaymentConfiguration : IEntityTypeConfiguration<SurveyPayment>
{
    public void Configure(EntityTypeBuilder<SurveyPayment> builder)
    {
        builder.ToTable(
            "survey_payments",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_payments_amount_positive", "amount > 0");
                table.HasCheckConstraint("ck_survey_payments_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_survey_payments_confirmation", "(status = 'CONFIRMED'::system.survey_payment_status AND confirmed_at IS NOT NULL AND provider_reference IS NOT NULL) OR (status <> 'CONFIRMED'::system.survey_payment_status)");
            });
        builder.HasKey(payment => payment.Id).HasName("pk_survey_payments");
        builder.Property(payment => payment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(payment => payment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(payment => payment.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(payment => payment.Provider).HasColumnName("provider").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(payment => payment.ProviderReference).HasColumnName("provider_reference").HasColumnType("character varying(150)").HasMaxLength(150);
        builder.Property(payment => payment.Status).HasColumnName("status").HasColumnType("system.survey_payment_status").IsRequired();
        builder.Property(payment => payment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(payment => payment.Version).IsRowVersion();
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderReference }).HasDatabaseName("uq_survey_payments_provider_reference").HasFilter("provider_reference IS NOT NULL").IsUnique();
        builder.HasIndex(payment => payment.SurveyOrderId).HasDatabaseName("uq_survey_payments_one_active_per_order").HasFilter("status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'CONFIRMED'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)").IsUnique();
        builder.HasIndex(payment => new { payment.Status, payment.UpdatedAt }).HasDatabaseName("ix_survey_payments_reconciliation");
        builder.HasOne(payment => payment.SurveyOrder).WithMany(order => order.Payments).HasForeignKey(payment => payment.SurveyOrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_payments_orders_order_id");
    }
}

