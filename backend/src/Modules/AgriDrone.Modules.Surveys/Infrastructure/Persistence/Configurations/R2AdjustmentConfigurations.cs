using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class ScopeAdjustmentConfiguration : IEntityTypeConfiguration<ScopeAdjustment>
{
    public void Configure(EntityTypeBuilder<ScopeAdjustment> builder)
    {
        builder.ToTable("scope_adjustments", "survey");
        builder.HasKey(item => item.Id).HasName("pk_scope_adjustments");
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id");
        builder.Property(item => item.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(80);
        builder.Property(item => item.BeforeBoundaryVersionId).HasColumnName("before_boundary_version_id");
        builder.Property(item => item.AfterBoundaryVersionId).HasColumnName("after_boundary_version_id");
        builder.Property(item => item.BeforePoleCount).HasColumnName("before_pole_count");
        builder.Property(item => item.AfterPoleCount).HasColumnName("after_pole_count");
        builder.Property(item => item.FinancialImpact).HasColumnName("financial_impact").HasPrecision(18, 2);
        builder.Property(item => item.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(item => item.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.RequestedBy).HasColumnName("requested_by");
        builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(item => item.ReviewReason).HasColumnName("review_reason").HasMaxLength(2000);
        builder.Property(item => item.Version).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(item => new { item.SurveyOrderId, item.CreatedAt }).HasDatabaseName("ix_scope_adjustments_order_timeline");
        builder.HasIndex(item => item.SurveyOrderId).HasFilter("status IN ('Pending', 'Approved')").IsUnique().HasDatabaseName("uq_scope_adjustments_open_order");
        builder.HasOne(item => item.SurveyOrder).WithMany().HasForeignKey(item => item.SurveyOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdjustmentSettlementConfiguration : IEntityTypeConfiguration<AdjustmentSettlement>
{
    public void Configure(EntityTypeBuilder<AdjustmentSettlement> builder)
    {
        builder.ToTable("adjustment_settlements", "survey", table =>
            table.HasCheckConstraint("ck_adjustment_settlements_amount", "amount > 0"));
        builder.HasKey(item => item.Id).HasName("pk_adjustment_settlements");
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PriceAdjustmentId).HasColumnName("price_adjustment_id");
        builder.Property(item => item.Type).HasColumnName("settlement_type").HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(item => item.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(item => item.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
        builder.Property(item => item.ProviderReference).HasColumnName("provider_reference").HasMaxLength(500);
        builder.Property(item => item.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
        builder.Property(item => item.Version).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(item => item.IdempotencyKey).IsUnique().HasDatabaseName("uq_adjustment_settlements_idempotency");
        builder.HasOne(item => item.PriceAdjustment).WithMany().HasForeignKey(item => item.PriceAdjustmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExtraFlightRequestConfiguration : IEntityTypeConfiguration<ExtraFlightRequest>
{
    public void Configure(EntityTypeBuilder<ExtraFlightRequest> builder)
    {
        builder.ToTable("extra_flight_requests", "survey", table =>
            table.HasCheckConstraint("ck_extra_flight_requests_cost", "cost_impact >= 0"));
        builder.HasKey(item => item.Id).HasName("pk_extra_flight_requests");
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id");
        builder.Property(item => item.FarmId).HasColumnName("farm_id");
        builder.Property(item => item.SourceMissionId).HasColumnName("source_mission_id");
        builder.Property(item => item.ExtraMissionId).HasColumnName("extra_mission_id");
        builder.Property(item => item.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(80);
        builder.Property(item => item.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.CostImpact).HasColumnName("cost_impact").HasPrecision(18, 2);
        builder.Property(item => item.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.RequestedBy).HasColumnName("requested_by");
        builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(item => item.Version).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(item => item.ExtraMissionId).IsUnique().HasDatabaseName("uq_extra_flight_requests_extra_mission");
        builder.HasIndex(item => new { item.SurveyOrderId, item.Status }).HasDatabaseName("ix_extra_flight_requests_order_status");
        builder.HasOne(item => item.SurveyOrder).WithMany().HasForeignKey(item => item.SurveyOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
