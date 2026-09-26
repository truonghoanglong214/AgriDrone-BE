using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyServicePriceConfiguration : IEntityTypeConfiguration<SurveyServicePrice>
{
    public void Configure(EntityTypeBuilder<SurveyServicePrice> builder)
    {
        builder.ToTable(
            "survey_service_prices",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_service_prices_amount_positive", "price_per_ha > 0");
                table.HasCheckConstraint("ck_survey_service_prices_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_survey_service_prices_window", "effective_to IS NULL OR effective_to > effective_from");
            });
        builder.HasKey(price => price.Id).HasName("pk_survey_service_prices");
        builder.HasAlternateKey(price => new { price.Id, price.SurveyServiceId }).HasName("uq_survey_service_prices_id_service");
        builder.Property(price => price.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(price => price.SurveyServiceId).HasColumnName("survey_service_id").HasColumnType("uuid");
        builder.Property(price => price.PricePerHa).HasColumnName("price_per_ha").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(price => price.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(price => price.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone");
        builder.Property(price => price.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(price => price.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(price => price.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(price => new { price.SurveyServiceId, price.EffectiveFrom }).HasDatabaseName("ix_survey_service_prices_effective").IsDescending(false, true);
        builder.HasOne(price => price.SurveyService).WithMany(service => service.Prices).HasForeignKey(price => price.SurveyServiceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_service_prices_services_service_id");
    }
}

