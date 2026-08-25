using AgriDrone.Modules.Harvests.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence.Configurations;

public sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable(
            "seasons",
            "harvest",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "A farm harvest/growing season used to aggregate productivity over time.");
                tableBuilder.HasCheckConstraint(
                    "ck_season_dates",
                    "end_date IS NULL OR end_date >= start_date");
                tableBuilder.HasCheckConstraint(
                    "ck_season_year",
                    "year BETWEEN 2000 AND 2200");
            });

        builder.HasKey(season => season.Id).HasName("pk_seasons");
        builder.HasAlternateKey(season => new { season.Id, season.FarmId })
            .HasName("uq_seasons_id_farm");

        builder.Property(season => season.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(season => season.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(season => season.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(season => season.Year)
            .HasColumnName("year")
            .HasColumnType("smallint");

        builder.Property(season => season.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(season => season.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

        builder.Property(season => season.Status)
            .HasColumnName("status")
            .HasColumnType("system.season_status")
            .HasSentinel((SeasonStatus)(-1))
            .HasDefaultValueSql("'PLANNED'::system.season_status")
            .IsRequired();

        builder.Property(season => season.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(season => season.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(season => new
        {
            season.FarmId,
            season.Name,
            season.StartDate
        })
            .HasDatabaseName("uq_seasons_farm_name_start")
            .IsUnique();

        builder.HasIndex(season => new { season.FarmId, season.StartDate })
            .HasDatabaseName("ix_seasons_farm")
            .IsDescending(false, true);
    }
}
