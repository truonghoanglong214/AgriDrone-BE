using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class PriceAdjustmentConfiguration : IEntityTypeConfiguration<PriceAdjustment>
{
    public void Configure(EntityTypeBuilder<PriceAdjustment> builder)
    {
        builder.ToTable(
            "price_adjustments",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_price_adjustments_area_positive", "old_area_ha > 0 AND new_area_ha > 0");
                table.HasCheckConstraint("ck_price_adjustments_price_nonnegative", "old_price >= 0 AND new_price >= 0");
                table.HasCheckConstraint("ck_price_adjustments_changed", "old_area_ha <> new_area_ha OR old_price <> new_price");
                table.HasCheckConstraint("ck_price_adjustments_approval", "(status IN ('APPROVED'::system.price_adjustment_status, 'APPLIED'::system.price_adjustment_status) AND approved_by IS NOT NULL AND approved_at IS NOT NULL) OR status IN ('PENDING'::system.price_adjustment_status, 'REJECTED'::system.price_adjustment_status)");
            });
        builder.HasKey(adjustment => adjustment.Id).HasName("pk_price_adjustments");
        builder.Property(adjustment => adjustment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(adjustment => adjustment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(adjustment => adjustment.OldAreaHa).HasColumnName("old_area_ha").HasColumnType("numeric(12,4)").HasPrecision(12, 4);
        builder.Property(adjustment => adjustment.NewAreaHa).HasColumnName("new_area_ha").HasColumnType("numeric(12,4)").HasPrecision(12, 4);
        builder.Property(adjustment => adjustment.OldPrice).HasColumnName("old_price").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.NewPrice).HasColumnName("new_price").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.Reason).HasColumnName("reason").HasColumnType("text").IsRequired();
        builder.Property(adjustment => adjustment.Status).HasColumnName("status").HasColumnType("system.price_adjustment_status").IsRequired();
        builder.Property(adjustment => adjustment.RequestedBy).HasColumnName("requested_by").HasColumnType("uuid");
        builder.Property(adjustment => adjustment.ApprovedBy).HasColumnName("approved_by").HasColumnType("uuid");
        builder.Property(adjustment => adjustment.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamp with time zone");
        builder.Property(adjustment => adjustment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(adjustment => new { adjustment.SurveyOrderId, adjustment.CreatedAt }).HasDatabaseName("ix_price_adjustments_order_timeline");
        builder.HasIndex(adjustment => adjustment.SurveyOrderId).HasDatabaseName("uq_price_adjustments_one_pending_per_order").HasFilter("status = 'PENDING'::system.price_adjustment_status").IsUnique();
        builder.HasOne(adjustment => adjustment.SurveyOrder).WithMany().HasForeignKey(adjustment => adjustment.SurveyOrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_price_adjustments_orders_order_id");
    }
}

