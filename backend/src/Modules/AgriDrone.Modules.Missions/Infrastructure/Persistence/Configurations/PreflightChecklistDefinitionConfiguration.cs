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
