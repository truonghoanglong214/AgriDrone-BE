using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantScanConfiguration : IEntityTypeConfiguration<PlantScan>
{
    public void Configure(EntityTypeBuilder<PlantScan> builder)
    {
        builder.ToTable(
            "plant_scans",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Time-series health observation of a plant from drone AI, manager, or field worker.");
                tableBuilder.HasCheckConstraint(
                    "ck_scan_confidence",
                    "overall_confidence IS NULL OR overall_confidence BETWEEN 0 AND 1");
            });

        builder.HasKey(scan => scan.Id).HasName("pk_plant_scans");
        builder.HasAlternateKey(scan => new { scan.Id, scan.FarmId })
            .HasName("uq_plant_scans_id_farm");
        builder.HasAlternateKey(scan => new { scan.Id, scan.PlantId, scan.FarmId })
            .HasName("uq_plant_scans_id_plant_farm");

        builder.Property(scan => scan.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(scan => scan.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.PlantId)
            .HasColumnName("plant_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.AiJobId)
            .HasColumnName("ai_job_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.VerificationOfScanId)
            .HasColumnName("verification_of_scan_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.SourceTaskId)
            .HasColumnName("source_task_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.ClientOperationId)
            .HasColumnName("client_operation_id")
            .HasColumnType("uuid");

        builder.Property(scan => scan.ObservedAt)
            .HasColumnName("observed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(scan => scan.Source)
            .HasColumnName("source")
            .HasColumnType("system.scan_source")
            .IsRequired();

        builder.Property(scan => scan.OverallHealthLevelId)
            .HasColumnName("overall_health_level_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(scan => scan.OverallConfidence)
            .HasColumnName("overall_confidence")
            .HasColumnType("numeric(5,4)")
            .HasPrecision(5, 4);

        builder.Property(scan => scan.Notes)
            .HasColumnName("notes")
            .HasColumnType("text");

        builder.Property(scan => scan.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(scan => scan.DeviceCreatedAt)
            .HasColumnName("device_created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(scan => scan.ServerReceivedAt)
            .HasColumnName("server_received_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(scan => scan.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(scan => new { scan.PlantId, scan.ObservedAt })
            .HasDatabaseName("ix_plant_scans_plant_date")
            .IsDescending(false, true);

        builder.HasIndex(scan => scan.MissionId)
            .HasDatabaseName("ix_plant_scans_mission");

        builder.HasIndex(scan => scan.ClientOperationId)
            .HasDatabaseName("uq_plant_scans_client_operation")
            .HasFilter("client_operation_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(scan => new
        {
            scan.FarmId,
            scan.OverallHealthLevelId,
            scan.ObservedAt
        })
            .HasDatabaseName("ix_plant_scans_farm_health_level")
            .IsDescending(false, false, true);

        builder.HasOne(scan => scan.Plant)
            .WithMany(plant => plant.Scans)
            .HasForeignKey(scan => new { scan.PlantId, scan.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scan_plant_same_farm");

        builder.HasOne(scan => scan.OverallHealthLevel)
            .WithMany(level => level.PlantScans)
            .HasForeignKey(scan => scan.OverallHealthLevelId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scans_health_levels_overall_health_level_id");

        builder.HasOne(scan => scan.VerificationOfScan)
            .WithMany(scan => scan.VerificationScans)
            .HasForeignKey(scan => new
            {
                scan.VerificationOfScanId,
                scan.PlantId,
                scan.FarmId
            })
            .HasPrincipalKey(original => new
            {
                original.Id,
                original.PlantId,
                original.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scans_verification_of_same_plant_farm");
    }
}
