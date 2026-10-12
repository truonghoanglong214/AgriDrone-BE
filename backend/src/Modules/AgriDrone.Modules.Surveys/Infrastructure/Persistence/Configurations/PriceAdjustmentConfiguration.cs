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
                table.HasCheckConstraint(
                    "ck_price_adjustments_pole_count_positive",
                    "old_pole_count > 0 AND new_pole_count > 0");
                table.HasCheckConstraint(
                    "ck_price_adjustments_price_positive",
                    "old_price_per_pole > 0 AND new_price_per_pole > 0 AND old_final_price > 0 AND new_final_price > 0");
                table.HasCheckConstraint(
                    "ck_price_adjustments_currency",
                    "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "ck_price_adjustments_changed",
                    "old_area_ha IS NOT NULL OR old_pole_count <> new_pole_count OR old_survey_service_price_id <> new_survey_service_price_id OR old_price_per_pole <> new_price_per_pole");
                table.HasCheckConstraint(
                    "ck_price_adjustments_final_price",
                    "old_final_price = round(old_pole_count * old_price_per_pole, 2) AND new_final_price = round(new_pole_count * new_price_per_pole, 2)");
                table.HasCheckConstraint(
                    "ck_price_adjustments_review",
                    "(status = 'PENDING'::system.price_adjustment_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence_reference IS NULL) OR (status IN ('APPROVED'::system.price_adjustment_status, 'REJECTED'::system.price_adjustment_status, 'APPLIED'::system.price_adjustment_status) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence_reference IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_price_adjustments_application",
                    "(status = 'APPLIED'::system.price_adjustment_status AND applied_by IS NOT NULL AND applied_at IS NOT NULL) OR (status <> 'APPLIED'::system.price_adjustment_status AND applied_by IS NULL AND applied_at IS NULL)");
            });
        builder.HasKey(adjustment => adjustment.Id)
            .HasName("pk_price_adjustments");
        builder.Property(adjustment => adjustment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();
        builder.Property(adjustment => adjustment.SurveyOrderId)
            .HasColumnName("survey_order_id")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.OldPoleCount)
            .HasColumnName("old_pole_count")
            .HasColumnType("integer");
        builder.Property(adjustment => adjustment.NewPoleCount)
            .HasColumnName("new_pole_count")
            .HasColumnType("integer");
        builder.Property(adjustment => adjustment.OldSurveyServicePriceId)
            .HasColumnName("old_survey_service_price_id")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.NewSurveyServicePriceId)
            .HasColumnName("new_survey_service_price_id")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.OldPricePerPole)
            .HasColumnName("old_price_per_pole")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.NewPricePerPole)
            .HasColumnName("new_price_per_pole")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.OldFinalPrice)
            .HasColumnName("old_final_price")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.NewFinalPrice)
            .HasColumnName("new_final_price")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.Currency)
            .HasColumnName("currency")
            .HasColumnType("character(3)")
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();
        builder.Property(adjustment => adjustment.Reason)
            .HasColumnName("reason")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(adjustment => adjustment.EvidenceReference)
            .HasColumnName("evidence_reference")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(adjustment => adjustment.Status)
            .HasColumnName("status")
            .HasColumnType("system.price_adjustment_status")
            .IsRequired();
        builder.Property(adjustment => adjustment.RequestedBy)
            .HasColumnName("requested_by")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(adjustment => adjustment.ReviewReason)
            .HasColumnName("review_reason")
            .HasColumnType("text");
        builder.Property(adjustment => adjustment.ReviewEvidenceReference)
            .HasColumnName("review_evidence_reference")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000);
        builder.Property(adjustment => adjustment.AppliedBy)
            .HasColumnName("applied_by")
            .HasColumnType("uuid");
        builder.Property(adjustment => adjustment.AppliedAt)
            .HasColumnName("applied_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(adjustment => adjustment.Version).IsRowVersion();
        builder.Property(adjustment => adjustment.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();
        // Retain the pre-4E area-based record verbatim for upgrade history. New
        // writes never populate these shadow columns.
        builder.Property<decimal?>("LegacyOldAreaHa")
            .HasColumnName("old_area_ha")
            .HasColumnType("numeric(12,4)")
            .HasPrecision(12, 4);
        builder.Property<decimal?>("LegacyNewAreaHa")
            .HasColumnName("new_area_ha")
            .HasColumnType("numeric(12,4)")
            .HasPrecision(12, 4);
        builder.Property<decimal?>("LegacyOldPrice")
            .HasColumnName("old_price")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property<decimal?>("LegacyNewPrice")
            .HasColumnName("new_price")
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, 2);
        builder.Property<Guid?>("LegacyApprovedBy")
            .HasColumnName("approved_by")
            .HasColumnType("uuid");
        builder.Property<DateTimeOffset?>("LegacyApprovedAt")
            .HasColumnName("approved_at")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(adjustment => new
            {
                adjustment.SurveyOrderId,
                adjustment.CreatedAt
            })
            .HasDatabaseName("ix_price_adjustments_order_timeline");
        builder.HasIndex(adjustment => adjustment.SurveyOrderId)
            .HasDatabaseName("uq_price_adjustments_one_open_per_order")
            .HasFilter(
                "status IN ('PENDING'::system.price_adjustment_status, 'APPROVED'::system.price_adjustment_status)")
            .IsUnique();
        builder.HasOne(adjustment => adjustment.SurveyOrder)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_price_adjustments_orders_order_id");
    }
}
