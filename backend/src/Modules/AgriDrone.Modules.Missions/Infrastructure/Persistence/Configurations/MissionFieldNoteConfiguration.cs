using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionFieldNoteConfiguration : IEntityTypeConfiguration<MissionFieldNote>
{
    public void Configure(EntityTypeBuilder<MissionFieldNote> builder)
    {
        builder.ToTable("mission_field_notes", "mission", table =>
        {
            table.HasCheckConstraint("ck_mission_field_notes_text", "length(trim(text)) > 0");
            table.HasCheckConstraint("ck_mission_field_notes_incident",
                "(incident_type IS NULL AND incident_outcome IS NULL AND " +
                "recovery_decision IS NULL AND evidence_reference IS NULL) OR " +
                "(incident_type IN ('SIGNAL_LOSS','LOW_BATTERY','INTERRUPTION','FLIGHT_FAILURE') " +
                "AND incident_outcome IS NOT NULL AND recovery_decision IS NOT NULL " +
                "AND evidence_reference IS NOT NULL " +
                "AND length(trim(incident_outcome)) > 0 AND " +
                "recovery_decision IN ('CONTINUE','ABORT','RESCHEDULE_REQUIRED') AND " +
                "length(trim(evidence_reference)) > 0)");
        });
        builder.HasKey(note => note.Id).HasName("pk_mission_field_notes");
        builder.Property(note => note.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(note => note.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(note => note.FarmId).HasColumnName("farm_id").IsRequired();
        builder.Property(note => note.MissionId).HasColumnName("mission_id").IsRequired();
        builder.Property(note => note.OperationId).HasColumnName("operation_id").IsRequired();
        builder.Property(note => note.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(note => note.Text).HasColumnName("text").HasMaxLength(2000).IsRequired();
        builder.Property(note => note.ObservedAt).HasColumnName("observed_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(note => note.ReceivedAt).HasColumnName("received_at")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(note => note.IncidentType).HasColumnName("incident_type")
            .HasColumnType("character varying(32)").HasMaxLength(32);
        builder.Property(note => note.IncidentOutcome).HasColumnName("incident_outcome")
            .HasColumnType("character varying(1000)").HasMaxLength(1000);
        builder.Property(note => note.RecoveryDecision).HasColumnName("recovery_decision")
            .HasColumnType("character varying(32)").HasMaxLength(32);
        builder.Property(note => note.EvidenceReference).HasColumnName("evidence_reference")
            .HasColumnType("character varying(500)").HasMaxLength(500);
        builder.HasIndex(note => new { note.MissionId, note.OperationId })
            .IsUnique().HasDatabaseName("uq_mission_field_notes_operation");
        builder.HasIndex(note => new { note.MissionId, note.ObservedAt })
            .HasDatabaseName("ix_mission_field_notes_timeline");
        builder.HasOne<DroneMission>().WithMany()
            .HasForeignKey(note => new { note.MissionId, note.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mission_field_notes_mission_farm");
        builder.HasOne<DroneMission>().WithMany()
            .HasForeignKey(note => new { note.MissionId, note.TenantId })
            .HasPrincipalKey(mission => new { mission.Id, mission.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mission_field_notes_mission_tenant");
    }
}
