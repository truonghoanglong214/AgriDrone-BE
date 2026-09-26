using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyOrderConfiguration : IEntityTypeConfiguration<SurveyOrder>
{
    public void Configure(EntityTypeBuilder<SurveyOrder> builder)
    {
        builder.ToTable(
            "survey_orders",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_orders_area_positive", "confirmed_survey_area_ha IS NULL OR confirmed_survey_area_ha > 0");
                table.HasCheckConstraint("ck_survey_orders_price_nonnegative", "(price_per_ha_snapshot IS NULL OR price_per_ha_snapshot > 0) AND (final_price IS NULL OR final_price >= 0)");
                table.HasCheckConstraint("ck_survey_orders_currency", "currency IS NULL OR currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "ck_survey_orders_pricing_snapshot_complete",
                    "(confirmed_survey_area_ha IS NULL AND price_per_ha_snapshot IS NULL AND currency IS NULL AND final_price IS NULL AND scope_confirmed_by IS NULL AND scope_confirmed_at IS NULL) OR " +
                    "(confirmed_survey_area_ha IS NOT NULL AND price_per_ha_snapshot IS NOT NULL AND currency IS NOT NULL AND final_price IS NOT NULL AND scope_confirmed_by IS NOT NULL AND scope_confirmed_at IS NOT NULL)");
                table.HasCheckConstraint("ck_survey_orders_final_price", "final_price IS NULL OR final_price = round(confirmed_survey_area_ha * price_per_ha_snapshot, 2)");
                table.HasCheckConstraint("ck_survey_orders_previous_not_self", "previous_compatible_order_id IS NULL OR previous_compatible_order_id <> id");
            });
        builder.HasKey(order => order.Id).HasName("pk_survey_orders");
        builder.HasAlternateKey(order => new { order.Id, order.TenantId, order.FarmId }).HasName("uq_survey_orders_id_tenant_farm");
        builder.HasAlternateKey(order => new { order.Id, order.FarmId }).HasName("uq_survey_orders_id_farm");
        builder.HasAlternateKey(order => new { order.Id, order.FarmId, order.SurveyServiceId }).HasName("uq_survey_orders_id_farm_service");
        builder.Property(order => order.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(order => order.OrderNumber).HasColumnName("order_number").HasColumnType("character varying(40)").HasMaxLength(40).IsRequired();
        builder.Property(order => order.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(order => order.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(order => order.SurveyRequestId).HasColumnName("survey_request_id").HasColumnType("uuid");
        builder.Property(order => order.SurveyServiceId).HasColumnName("survey_service_id").HasColumnType("uuid");
        builder.Property(order => order.SurveyServicePriceId).HasColumnName("survey_service_price_id").HasColumnType("uuid");
        builder.Property(order => order.ConfirmedSurveyAreaHa).HasColumnName("confirmed_survey_area_ha").HasColumnType("numeric(12,4)").HasPrecision(12, 4);
        builder.Property(order => order.PricePerHaSnapshot).HasColumnName("price_per_ha_snapshot").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(order => order.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength();
        builder.Property(order => order.FinalPrice).HasColumnName("final_price").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(order => order.ScopeConfirmedBy).HasColumnName("scope_confirmed_by").HasColumnType("uuid");
        builder.Property(order => order.ScopeConfirmedAt).HasColumnName("scope_confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(order => order.RequiresBaselineMapping).HasColumnName("requires_baseline_mapping").HasColumnType("boolean");
        builder.Property(order => order.PreviousCompatibleOrderId).HasColumnName("previous_compatible_order_id").HasColumnType("uuid");
        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasColumnType("system.survey_order_status")
            .HasSentinel((SurveyOrderStatus)(-1))
            .HasDefaultValueSql("'PENDING_SCOPE_CONFIRMATION'::system.survey_order_status")
            .IsRequired();
        builder.Property(order => order.Version).IsRowVersion();
        builder.Property(order => order.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(order => order.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(order => order.OrderNumber).HasDatabaseName("uq_survey_orders_number").IsUnique();
        builder.HasIndex(order => order.SurveyRequestId).HasDatabaseName("uq_survey_orders_request").IsUnique();
        builder.HasIndex(order => new { order.TenantId, order.FarmId, order.Status }).HasDatabaseName("ix_survey_orders_manager_work_queue");
        builder.HasIndex(order => new { order.FarmId, order.SurveyServiceId, order.CreatedAt }).HasDatabaseName("ix_survey_orders_farm_service_history").IsDescending(false, false, true);
        builder.HasOne(order => order.SurveyRequest).WithOne().HasForeignKey<SurveyOrder>(order => order.SurveyRequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_orders_requests_request_id");
        builder.HasOne(order => order.SurveyService).WithMany().HasForeignKey(order => order.SurveyServiceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_orders_services_service_id");
        builder.HasOne(order => order.SurveyServicePrice).WithMany().HasForeignKey(order => new { order.SurveyServicePriceId, order.SurveyServiceId }).HasPrincipalKey(price => new { price.Id, price.SurveyServiceId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_orders_price_same_service");
        builder.HasOne(order => order.PreviousCompatibleOrder).WithMany().HasForeignKey(order => new { order.PreviousCompatibleOrderId, order.FarmId, order.SurveyServiceId }).HasPrincipalKey(order => new { order.Id, order.FarmId, order.SurveyServiceId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_orders_previous_same_farm_service");
    }
}
