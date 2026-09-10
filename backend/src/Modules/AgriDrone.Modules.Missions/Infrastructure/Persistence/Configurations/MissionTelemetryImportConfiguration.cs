using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure
    .Persistence.Configurations;

public sealed class MissionTelemetryImportConfiguration
    : IEntityTypeConfiguration<MissionTelemetryImport>
{
    public void Configure(
        EntityTypeBuilder<MissionTelemetryImport> builder)
    {
        builder.ToTable(
            "mission_telemetry_imports",
            "mission",
            table =>
            {
                table.HasComment(
                    "Idempotency record for an atomic " +
                    "normalized telemetry import.");

                table.HasCheckConstraint(
                    "ck_telemetry_imports_point_count",
                    "point_count >= 2");

                table.HasCheckConstraint(
                    "ck_telemetry_imports_checksum",
                    "payload_checksum ~ '^[0-9a-f]{64}$'");

                table.HasCheckConstraint(
                    "ck_telemetry_imports_time_range",
                    "last_recorded_at > first_recorded_at");
            });

        builder.HasKey(telemetryImport =>
                telemetryImport.Id)
            .HasName("pk_mission_telemetry_imports");

        builder.Property(telemetryImport =>
                telemetryImport.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(telemetryImport =>
                telemetryImport.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.OperationId)
            .HasColumnName("operation_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.SourceFileName)
            .HasColumnName("source_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.PayloadChecksum)
            .HasColumnName("payload_checksum")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.PointCount)
            .HasColumnName("point_count")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.FirstRecordedAt)
            .HasColumnName("first_recorded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.LastRecordedAt)
            .HasColumnName("last_recorded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(telemetryImport =>
                telemetryImport.ImportedAt)
            .HasColumnName("imported_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(telemetryImport => new
        {
            telemetryImport.TenantId,
            telemetryImport.MissionId,
            telemetryImport.OperationId
        })
            .IsUnique()
            .HasDatabaseName(
                "uq_telemetry_imports_operation");

        builder.HasIndex(telemetryImport =>
                telemetryImport.MissionId)
            .IsUnique()
            .HasDatabaseName(
                "uq_telemetry_imports_mission");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(telemetryImport => new
            {
                telemetryImport.MissionId,
                telemetryImport.FarmId
            })
            .HasPrincipalKey(mission => new
            {
                mission.Id,
                mission.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_telemetry_imports_mission_farm");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(telemetryImport => new
            {
                telemetryImport.MissionId,
                telemetryImport.TenantId
            })
            .HasPrincipalKey(mission => new
            {
                mission.Id,
                mission.TenantId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_telemetry_imports_mission_tenant");
    }
}