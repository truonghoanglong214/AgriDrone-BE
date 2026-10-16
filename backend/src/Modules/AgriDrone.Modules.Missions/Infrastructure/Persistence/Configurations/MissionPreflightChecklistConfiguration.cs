using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class MissionPreflightChecklistConfiguration : IEntityTypeConfiguration<MissionPreflightChecklist>
{
    public void Configure(EntityTypeBuilder<MissionPreflightChecklist> builder)
    {
        builder.ToTable(
            "mission_preflight_checklists",
            "mission",
            table =>
            {
                table.HasCheckConstraint("ck_mission_preflight_snapshot_object", "jsonb_typeof(definition_snapshot) = 'array'");
                table.HasCheckConstraint("ck_mission_preflight_responses_object", "jsonb_typeof(responses) = 'object'");
                table.HasCheckConstraint(
                    "ck_mission_preflight_completion",
                    "(status = 'COMPLETED'::system.mission_preflight_checklist_status AND completed_by IS NOT NULL AND completed_at IS NOT NULL) OR " +
                    "(status <> 'COMPLETED'::system.mission_preflight_checklist_status)");
            });
        builder.HasKey(checklist => checklist.Id).HasName("pk_mission_preflight_checklists");
        builder.Property(checklist => checklist.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(checklist => checklist.MissionId).HasColumnName("mission_id").HasColumnType("uuid");
        builder.Property(checklist => checklist.ChecklistDefinitionId).HasColumnName("checklist_definition_id").HasColumnType("uuid");
        builder.Property(checklist => checklist.ClientOperationId).HasColumnName("client_operation_id").HasColumnType("uuid");
        builder.Property(checklist => checklist.Status).HasColumnName("status").HasColumnType("system.mission_preflight_checklist_status").IsRequired();
        builder.Property(checklist => checklist.DefinitionSnapshot).HasColumnName("definition_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(checklist => checklist.Responses).HasColumnName("responses").HasColumnType("jsonb").IsRequired();
        builder.Property(checklist => checklist.UnsuitableConditionNotes).HasColumnName("unsuitable_condition_notes").HasColumnType("text");
        builder.Property(checklist => checklist.FailsafeNotes).HasColumnName("failsafe_notes").HasColumnType("text");
        builder.Property(checklist => checklist.FlightAuthorizationEvidence)
            .HasColumnName("flight_authorization_evidence").HasColumnType("text");
        builder.Property(checklist => checklist.CompletedBy).HasColumnName("completed_by").HasColumnType("uuid");
        builder.Property(checklist => checklist.DeviceCompletedAt).HasColumnName("device_completed_at").HasColumnType("timestamp with time zone");
        builder.Property(checklist => checklist.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(checklist => checklist.ServerReceivedAt).HasColumnName("server_received_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(checklist => checklist.Version).IsRowVersion();
        builder.HasIndex(checklist => checklist.ClientOperationId).HasDatabaseName("uq_mission_preflight_client_operation").IsUnique();
        builder.HasIndex(checklist => new { checklist.MissionId, checklist.ChecklistDefinitionId }).HasDatabaseName("uq_mission_preflight_definition").IsUnique();
        builder.HasIndex(checklist => checklist.MissionId).HasDatabaseName("uq_mission_preflight_one_completed").HasFilter("status = 'COMPLETED'::system.mission_preflight_checklist_status").IsUnique();
        builder.HasOne(checklist => checklist.Mission).WithMany(mission => mission.PreflightChecklists).HasForeignKey(checklist => checklist.MissionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mission_preflight_checklists_missions_mission_id");
        builder.HasOne(checklist => checklist.ChecklistDefinition).WithMany(definition => definition.MissionChecklists).HasForeignKey(checklist => checklist.ChecklistDefinitionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mission_preflight_checklists_definitions_definition_id");
    }
}
