using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Harvests.Infrastructure.Persistence.Configurations;

public sealed class PlantHarvestRecordConfiguration
    : IEntityTypeConfiguration<PlantHarvestRecord>
{
    public void Configure(EntityTypeBuilder<PlantHarvestRecord> builder)
    {
        builder.ToTable(
            "plant_harvest_records",
            "harvest",
            tableBuilder =>
            {
                tableBuilder.HasComment("Per-plant yield record within a harvest batch.");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_harvest_fruit_count",
                    "fruit_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_harvest_weight",
                    "weight_kg >= 0");
            });

        builder.HasKey(record => record.Id).HasName("pk_plant_harvest_records");
        builder.HasAlternateKey(record => new { record.Id, record.FarmId })
            .HasName("uq_plant_harvest_records_id_farm");

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(record => record.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(record => record.HarvestBatchId)
            .HasColumnName("harvest_batch_id")
            .HasColumnType("uuid");

        builder.Property(record => record.PlantId)
            .HasColumnName("plant_id")
            .HasColumnType("uuid");

        builder.Property(record => record.FruitCount)
            .HasColumnName("fruit_count")
            .HasColumnType("integer");

        builder.Property(record => record.WeightKg)
            .HasColumnName("weight_kg")
            .HasColumnType("numeric(10,3)")
            .HasPrecision(10, 3);

        builder.Property(record => record.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(record => record.RecordedBy)
            .HasColumnName("recorded_by")
            .HasColumnType("uuid");

        builder.Property(record => record.RecordedAt)
            .HasColumnName("recorded_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(record => record.Source)
            .HasColumnName("source")
            .HasColumnType("system.harvest_record_source")
            .HasDefaultValueSql("'WEB'::system.harvest_record_source")
            .IsRequired();

        builder.Property(record => record.ClientOperationId)
            .HasColumnName("client_operation_id")
            .HasColumnType("uuid");

        builder.Property(record => record.DeviceCreatedAt)
            .HasColumnName("device_created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(record => record.ServerReceivedAt)
            .HasColumnName("server_received_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(record => record.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(record => new { record.HarvestBatchId, record.PlantId })
            .HasDatabaseName("uq_plant_harvest_batch_plant")
            .IsUnique();

        builder.HasIndex(record => record.PlantId)
            .HasDatabaseName("ix_plant_harvest_records_plant");

        builder.HasIndex(record => record.HarvestBatchId)
            .HasDatabaseName("ix_plant_harvest_records_batch");

        builder.HasIndex(record => record.ClientOperationId)
            .HasDatabaseName("uq_plant_harvest_records_client_operation")
            .HasFilter("client_operation_id IS NOT NULL")
            .IsUnique();

        builder.HasOne(record => record.HarvestBatch)
            .WithMany(batch => batch.PlantHarvestRecords)
            .HasForeignKey(record => new { record.HarvestBatchId, record.FarmId })
            .HasPrincipalKey(batch => new { batch.Id, batch.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_plant_harvest_batch_same_farm");
    }
}
