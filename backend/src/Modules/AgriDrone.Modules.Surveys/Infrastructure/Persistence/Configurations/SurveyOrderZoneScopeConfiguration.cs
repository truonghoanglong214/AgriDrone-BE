using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyOrderZoneScopeConfiguration
    : IEntityTypeConfiguration<SurveyOrderZoneScope>
{
    public void Configure(EntityTypeBuilder<SurveyOrderZoneScope> builder)
    {
        builder.ToTable("survey_order_zone_scopes", "survey");
        builder.HasKey(scope => scope.Id)
            .HasName("pk_survey_order_zone_scopes");
        builder.Property(scope => scope.Id)
            .HasColumnName("id").HasColumnType("uuid");
        builder.Property(scope => scope.SurveyOrderId)
            .HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(scope => scope.FarmId)
            .HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(scope => scope.ZoneId)
            .HasColumnName("zone_id").HasColumnType("uuid");
        builder.Property(scope => scope.ZoneVersion)
            .HasColumnName("zone_version").HasColumnType("bigint");
        builder.Property(scope => scope.BoundarySnapshot)
            .HasColumnName("boundary_snapshot")
            .HasColumnType("geometry(Polygon,4326)")
            .IsRequired();
        builder.Property(scope => scope.CapturedAt)
            .HasColumnName("captured_at")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(scope => new { scope.SurveyOrderId, scope.ZoneId })
            .HasDatabaseName("uq_survey_order_zone_scopes_order_zone")
            .IsUnique();
        builder.HasIndex(scope => new { scope.FarmId, scope.ZoneId })
            .HasDatabaseName("ix_survey_order_zone_scopes_farm_zone");
    }
}
