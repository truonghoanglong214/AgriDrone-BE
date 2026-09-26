using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyServiceConfiguration : IEntityTypeConfiguration<SurveyService>
{
    public static readonly Guid PlantHealthId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid HarvestReadinessId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<SurveyService> builder)
    {
        builder.ToTable("survey_services", "survey");
        builder.HasKey(service => service.Id).HasName("pk_survey_services");
        builder.Property(service => service.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(service => service.Code).HasColumnName("code").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(service => service.Name).HasColumnName("name").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(service => service.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(service => service.ServiceType).HasColumnName("service_type").HasColumnType("system.survey_service_type").IsRequired();
        builder.Property(service => service.Status).HasColumnName("status").HasColumnType("system.survey_service_status").IsRequired();
        builder.Property(service => service.Version).IsRowVersion();
        builder.Property(service => service.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(service => service.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(service => service.Code).HasDatabaseName("uq_survey_services_code").IsUnique();
        builder.HasIndex(service => new { service.Status, service.ServiceType }).HasDatabaseName("ix_survey_services_catalogue");

        var seededAt = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new
            {
                Id = PlantHealthId,
                Code = "PLANT_HEALTH",
                Name = "Plant Health Survey",
                Description = "Drone imagery survey for human-verified plant health findings.",
                ServiceType = SurveyServiceType.PlantHealth,
                Status = SurveyServiceStatus.Active,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new
            {
                Id = HarvestReadinessId,
                Code = "HARVEST_READINESS",
                Name = "Harvest Readiness Survey",
                Description = "Experimental visible-indicator assessment of harvest readiness.",
                ServiceType = SurveyServiceType.HarvestReadiness,
                Status = SurveyServiceStatus.Experimental,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            });
    }
}

