using AgriDrone.Modules.Plants.Domain.Conditions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class PlantConditionConfiguration : IEntityTypeConfiguration<PlantCondition>
{
    public void Configure(EntityTypeBuilder<PlantCondition> builder)
    {
        builder.ToTable(
            "plant_conditions",
            "plant",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Global catalog of diseases, abiotic damage and mechanical plant conditions.");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_conditions_health_label_metadata",
                    "(plant_health_label_kind IS NULL AND dataset_version IS NULL AND annotation_rule_version IS NULL AND team_label_limitation IS NULL) OR " +
                    "(plant_health_label_kind IS NOT NULL AND dataset_version IS NOT NULL AND annotation_rule_version IS NOT NULL AND team_label_limitation IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_conditions_health_label_kind",
                    "plant_health_label_kind IS NULL OR plant_health_label_kind IN ('SuspectedDisease', 'ConfusingNonDisease')");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_conditions_health_label_type",
                    "(plant_health_label_kind IS NULL) OR " +
                    "(plant_health_label_kind = 'SuspectedDisease' AND condition_type = 'DISEASE'::system.condition_type) OR " +
                    "(plant_health_label_kind = 'ConfusingNonDisease' AND condition_type <> 'DISEASE'::system.condition_type)");
                tableBuilder.HasCheckConstraint(
                    "ck_plant_conditions_health_label_code",
                    "plant_health_label_kind IS NULL OR " +
                    "(plant_health_label_kind = 'SuspectedDisease' AND code IN ('BROWN_SPOT', 'ANTHRACNOSE')) OR " +
                    "(plant_health_label_kind = 'ConfusingNonDisease' AND code IN ('SUNBURN', 'MECHANICAL_SCAR'))");
            });

        builder.HasKey(condition => condition.Id).HasName("pk_plant_conditions");
        builder.HasAlternateKey(condition => new
            {
                condition.Id,
                condition.RevisionNumber
            })
            .HasName("ak_plant_conditions_id_revision");

        builder.Property(condition => condition.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(condition => condition.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(condition => condition.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(condition => condition.ScientificName)
            .HasColumnName("scientific_name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150);

        builder.Property(condition => condition.ConditionType)
            .HasColumnName("condition_type")
            .HasColumnType("system.condition_type")
            .HasSentinel((ConditionType)(-1))
            .HasDefaultValueSql("'DISEASE'::system.condition_type")
            .IsRequired();

        builder.Property(condition => condition.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(condition => condition.PlantHealthLabelKind)
            .HasColumnName("plant_health_label_kind")
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(condition => condition.DatasetVersion)
            .HasColumnName("dataset_version")
            .HasMaxLength(100);

        builder.Property(condition => condition.AnnotationRuleVersion)
            .HasColumnName("annotation_rule_version")
            .HasMaxLength(100);

        builder.Property(condition => condition.TeamLabelLimitation)
            .HasColumnName("team_label_limitation")
            .HasMaxLength(1000);

        builder.Property(condition => condition.RevisionNumber)
            .HasColumnName("revision_number")
            .HasColumnType("integer")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(condition => condition.SupersedesId)
            .HasColumnName("supersedes_id")
            .HasColumnType("uuid");

        builder.Property(condition => condition.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(condition => condition.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(condition => condition.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(condition => condition.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(condition => condition.Version)
            .HasColumnName("version")
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(condition => new
            {
                condition.Code,
                condition.RevisionNumber
            })
            .HasDatabaseName("uq_plant_conditions_code_revision")
            .IsUnique();

        builder.HasIndex(condition => condition.Code)
            .HasDatabaseName("uq_plant_conditions_active_code")
            .HasFilter("is_active = TRUE")
            .IsUnique();

        builder.HasIndex(condition => condition.SupersedesId)
            .HasDatabaseName("uq_plant_conditions_supersedes")
            .HasFilter("supersedes_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(condition => new
            {
                condition.PlantHealthLabelKind,
                condition.IsActive
            })
            .HasDatabaseName("ix_plant_conditions_health_label_active")
            .HasFilter("plant_health_label_kind IS NOT NULL");

        builder.HasOne<PlantCondition>()
            .WithMany()
            .HasForeignKey(condition => condition.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_plant_conditions_superseded_condition_id");
    }
}
