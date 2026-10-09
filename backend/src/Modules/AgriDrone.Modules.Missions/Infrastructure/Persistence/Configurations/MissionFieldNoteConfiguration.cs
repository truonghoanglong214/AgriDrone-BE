using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionFieldNoteConfiguration : IEntityTypeConfiguration<MissionFieldNote>
{
    public void Configure(EntityTypeBuilder<MissionFieldNote> builder)
    {
        builder.ToTable("mission_field_notes", "mission", table =>
            table.HasCheckConstraint("ck_mission_field_notes_text", "length(trim(text)) > 0"));
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
