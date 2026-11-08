using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class QualityRefundCaseConfiguration : IEntityTypeConfiguration<QualityRefundCase>
{
    public void Configure(EntityTypeBuilder<QualityRefundCase> builder)
    {
        builder.ToTable("quality_refund_cases", "survey", table =>
        {
            table.HasCheckConstraint("ck_quality_refund_approved_amount", "approved_amount IS NULL OR approved_amount > 0");
            table.HasCheckConstraint("ck_quality_refund_transfer", "(status = 'Refunded' AND bank_transfer_reference IS NOT NULL AND transferred_at IS NOT NULL AND recorded_by IS NOT NULL AND recorded_at IS NOT NULL) OR (status <> 'Refunded' AND bank_transfer_reference IS NULL AND transferred_at IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.FarmId).HasColumnName("farm_id");
        builder.Property(item => item.SurveyResultId).HasColumnName("survey_result_id");
        builder.Property(item => item.ReportedBy).HasColumnName("reported_by");
        builder.Property(item => item.ReportedIssues).HasColumnName("reported_issues").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(item => item.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.RecipientBankName).HasColumnName("recipient_bank_name").HasMaxLength(150).IsRequired();
        builder.Property(item => item.RecipientAccountNumber).HasColumnName("recipient_account_number").HasMaxLength(34).IsRequired();
        builder.Property(item => item.RecipientAccountName).HasColumnName("recipient_account_name").HasMaxLength(150).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.ManagerFindings).HasColumnName("manager_findings").HasColumnType("jsonb");
        builder.Property(item => item.ManagerVerifiedBy).HasColumnName("manager_verified_by");
        builder.Property(item => item.ManagerVerifiedAt).HasColumnName("manager_verified_at");
        builder.Property(item => item.ManagerVerificationReason).HasColumnName("manager_verification_reason").HasMaxLength(2000);
        builder.Property(item => item.DecidedBy).HasColumnName("decided_by");
        builder.Property(item => item.DecidedAt).HasColumnName("decided_at");
        builder.Property(item => item.DecisionReason).HasColumnName("decision_reason").HasMaxLength(2000);
        builder.Property(item => item.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(18, 2);
        builder.Property(item => item.BankTransferReference).HasColumnName("bank_transfer_reference").HasMaxLength(150);
        builder.Property(item => item.TransferEvidenceReference).HasColumnName("transfer_evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.TransferredAt).HasColumnName("transferred_at");
        builder.Property(item => item.RecordedBy).HasColumnName("recorded_by");
        builder.Property(item => item.RecordedAt).HasColumnName("recorded_at");
        builder.Property(item => item.Version).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(item => item.SurveyOrderId).HasDatabaseName("uq_quality_refund_active_order")
            .IsUnique().HasFilter("status IN ('Submitted', 'Approved', 'Refunded')");
        builder.HasIndex(item => item.BankTransferReference).HasDatabaseName("uq_quality_refund_bank_reference")
            .IsUnique().HasFilter("bank_transfer_reference IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.CreatedAt });
        builder.HasOne(item => item.SurveyOrder).WithMany().HasForeignKey(item => item.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SurveyResult).WithMany().HasForeignKey(item => item.SurveyResultId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
