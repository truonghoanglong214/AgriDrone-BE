using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class PreflightChecklistDefinitionConfiguration : IEntityTypeConfiguration<PreflightChecklistDefinition>
{
    public void Configure(EntityTypeBuilder<PreflightChecklistDefinition> builder)
    {
        builder.ToTable(
            "preflight_checklist_definitions",
            "mission",
            table =>
            {
                table.HasCheckConstraint("ck_preflight_definitions_version_positive", "version_number >= 1");
                table.HasCheckConstraint("ck_preflight_definitions_items_array", "jsonb_typeof(items) = 'array'");
                table.HasCheckConstraint("ck_preflight_definitions_retirement", "(status = 'RETIRED'::system.preflight_checklist_definition_status AND retired_at IS NOT NULL) OR (status <> 'RETIRED'::system.preflight_checklist_definition_status AND retired_at IS NULL)");
            });
        builder.HasKey(definition => definition.Id).HasName("pk_preflight_checklist_definitions");
        builder.Property(definition => definition.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(definition => definition.Code).HasColumnName("code").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(definition => definition.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(definition => definition.Status).HasColumnName("status").HasColumnType("system.preflight_checklist_definition_status").IsRequired();
        builder.Property(definition => definition.Items).HasColumnName("items").HasColumnType("jsonb").IsRequired();
        builder.Property(definition => definition.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone");
        builder.Property(definition => definition.RetiredAt).HasColumnName("retired_at").HasColumnType("timestamp with time zone");
        builder.Property(definition => definition.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(definition => definition.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(definition => new { definition.Code, definition.VersionNumber }).HasDatabaseName("uq_preflight_definitions_code_version").IsUnique();
        builder.HasIndex(definition => definition.Code).HasDatabaseName("uq_preflight_definitions_one_active").HasFilter("status = 'ACTIVE'::system.preflight_checklist_definition_status").IsUnique();
    }
}

public sealed class MissionPreflightChecklistConfiguration : IEntityTypeConfiguration<MissionPreflightChecklist>
{
    public void Configure(EntityTypeBuilder<MissionPreflightChecklist> builder)
    {
        builder.ToTable(
            "mission_preflight_checklists",
            "mission",
            table =>
            {
                table.HasCheckConstraint("ck_mission_preflight_snapshot_object", "jsonb_typeof(definition_snapshot) = 'object'");
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
