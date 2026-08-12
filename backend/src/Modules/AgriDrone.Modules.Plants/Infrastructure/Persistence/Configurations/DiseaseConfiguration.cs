using AgriDrone.Modules.Plants.Domain.Diseases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class DiseaseConfiguration : IEntityTypeConfiguration<Disease>
{
    public void Configure(EntityTypeBuilder<Disease> builder)
    {
        builder.ToTable(
            "diseases",
            "plant",
            tableBuilder => tableBuilder.HasComment(
                "Configurable disease catalog. Disease types are data, not hard-coded columns."));

        builder.HasKey(disease => disease.Id).HasName("pk_diseases");

        builder.Property(disease => disease.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(disease => disease.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(disease => disease.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(disease => disease.ScientificName)
            .HasColumnName("scientific_name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150);

        builder.Property(disease => disease.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(disease => disease.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(disease => disease.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(disease => disease.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(disease => disease.Code)
            .HasDatabaseName("uq_diseases_code")
            .IsUnique();
    }
}
