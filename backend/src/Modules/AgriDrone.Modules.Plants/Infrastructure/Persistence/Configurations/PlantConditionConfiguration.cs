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
            tableBuilder => tableBuilder.HasComment(
                "Global catalog of diseases, abiotic damage and mechanical plant conditions."));

        builder.HasKey(condition => condition.Id).HasName("pk_plant_conditions");

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

        builder.HasOne<PlantCondition>()
            .WithMany()
            .HasForeignKey(condition => condition.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_plant_conditions_superseded_condition_id");
    }
}
