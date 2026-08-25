using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence.Configurations;

public sealed class HarvestBatchConfiguration : IEntityTypeConfiguration<HarvestBatch>
{
    public void Configure(EntityTypeBuilder<HarvestBatch> builder)
    {
        builder.ToTable(
            "harvest_batches",
            "harvest",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "One harvesting event; common data is entered once for the batch.");
                tableBuilder.HasCheckConstraint(
                    "ck_harvest_batch_fruit_count",
                    "reported_fruit_count IS NULL OR reported_fruit_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_harvest_batch_weight",
                    "reported_weight_kg IS NULL OR reported_weight_kg >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_harvest_batch_completion",
                    "(status = 'COMPLETED'::system.harvest_batch_status AND " +
                    "completed_by IS NOT NULL AND completed_at IS NOT NULL) OR " +
                    "(status <> 'COMPLETED'::system.harvest_batch_status AND " +
                    "completed_by IS NULL AND completed_at IS NULL)");
            });

        builder.HasKey(batch => batch.Id).HasName("pk_harvest_batches");
        builder.HasAlternateKey(batch => new { batch.Id, batch.FarmId })
            .HasName("uq_harvest_batches_id_farm");

        builder.Property(batch => batch.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(batch => batch.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(batch => batch.SeasonId)
            .HasColumnName("season_id")
            .HasColumnType("uuid");

        builder.Property(batch => batch.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(batch => batch.BatchCode)
            .HasColumnName("batch_code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(batch => batch.HarvestedAt)
            .HasColumnName("harvested_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(batch => batch.ReportedFruitCount)
            .HasColumnName("reported_fruit_count")
            .HasColumnType("integer");

        builder.Property(batch => batch.ReportedWeightKg)
            .HasColumnName("reported_weight_kg")
            .HasColumnType("numeric(12,3)")
            .HasPrecision(12, 3);

        builder.Property(batch => batch.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(batch => batch.Status)
            .HasColumnName("status")
            .HasColumnType("system.harvest_batch_status")
            .HasSentinel((HarvestBatchStatus)(-1))
            .HasDefaultValueSql("'DRAFT'::system.harvest_batch_status")
            .IsRequired();

        builder.Property(batch => batch.CompletedBy)
            .HasColumnName("completed_by")
            .HasColumnType("uuid");

        builder.Property(batch => batch.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(batch => batch.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(batch => batch.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(batch => batch.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(batch => batch.Version)
            .IsRowVersion();

        builder.HasIndex(batch => new { batch.FarmId, batch.BatchCode })
            .HasDatabaseName("uq_harvest_batches_farm_code")
            .IsUnique();

        builder.HasIndex(batch => new { batch.SeasonId, batch.HarvestedAt })
            .HasDatabaseName("ix_harvest_batches_season")
            .IsDescending(false, true);

        builder.HasIndex(batch => new { batch.ZoneId, batch.HarvestedAt })
            .HasDatabaseName("ix_harvest_batches_zone")
            .IsDescending(false, true);

        builder.HasOne(batch => batch.Season)
            .WithMany(season => season.HarvestBatches)
            .HasForeignKey(batch => new { batch.SeasonId, batch.FarmId })
            .HasPrincipalKey(season => new { season.Id, season.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_harvest_batch_season_same_farm");
    }
}
