using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyDepositObligationConfiguration
    : IEntityTypeConfiguration<SurveyDepositObligation>
{
    public void Configure(EntityTypeBuilder<SurveyDepositObligation> builder)
    {
        builder.ToTable(
            "survey_deposit_obligations",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_deposit_obligations_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint(
                    "ck_survey_deposit_obligations_allocated",
                    "allocated_amount >= 0 AND allocated_amount = FLOOR(allocated_amount)");
                table.HasCheckConstraint(
                    "ck_survey_deposit_obligations_currency",
                    "currency = 'VND'");
                table.HasCheckConstraint(
                    "ck_survey_deposit_obligations_status_time",
                    "(status = 'SATISFIED'::system.survey_deposit_obligation_status AND satisfied_at IS NOT NULL) OR " +
                    "(status = 'CANCELLED'::system.survey_deposit_obligation_status AND cancelled_at IS NOT NULL) OR " +
                    "(status = 'PENDING'::system.survey_deposit_obligation_status AND satisfied_at IS NULL AND cancelled_at IS NULL)");
            });

        builder.HasKey(obligation => obligation.Id)
            .HasName("pk_survey_deposit_obligations");
        builder.HasAlternateKey(obligation => new
            {
                obligation.Id,
                obligation.SurveyOrderId
            })
            .HasName("uq_survey_deposit_obligations_id_order");

        builder.Property(obligation => obligation.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(obligation => obligation.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.BusinessPolicyVersionId).HasColumnName("business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.DepositPolicyId).HasColumnName("deposit_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(obligation => obligation.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(obligation => obligation.AllocatedAmount).HasColumnName("allocated_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(obligation => obligation.Status).HasColumnName("status").HasColumnType("system.survey_deposit_obligation_status").IsRequired();
        builder.Property(obligation => obligation.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(obligation => obligation.SatisfiedAt).HasColumnName("satisfied_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(obligation => obligation.SurveyOrderId)
            .HasDatabaseName("uq_survey_deposit_obligations_order")
            .IsUnique();
        builder.HasIndex(obligation => new
            {
                obligation.SurveyOrderId,
                obligation.TenantId,
                obligation.FarmId
            })
            .HasDatabaseName("ix_survey_deposit_obligations_order_owner");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(obligation => new
            {
                obligation.SurveyOrderId,
                obligation.TenantId,
                obligation.FarmId
            })
            .HasPrincipalKey(order => new
            {
                order.Id,
                order.TenantId,
                order.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_deposit_obligations_order_same_owner");

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(obligation => obligation.BusinessPolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_deposit_obligations_business_policy");

        builder.HasOne<SurveyDepositPolicy>()
            .WithMany()
            .HasForeignKey(obligation => obligation.DepositPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_deposit_obligations_deposit_policy");
    }
}
