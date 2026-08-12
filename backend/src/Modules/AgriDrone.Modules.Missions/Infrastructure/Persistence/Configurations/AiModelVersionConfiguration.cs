using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class AiModelVersionConfiguration : IEntityTypeConfiguration<AiModelVersion>
{
    public void Configure(EntityTypeBuilder<AiModelVersion> builder)
    {
        builder.ToTable(
            "ai_model_versions",
            "mission",
            tableBuilder => tableBuilder.HasComment(
                "Version registry for AI models to guarantee result traceability and evaluation."));

        builder.HasKey(model => model.Id).HasName("pk_ai_model_versions");

        builder.Property(model => model.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(model => model.ModelName)
            .HasColumnName("model_name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(model => model.Version)
            .HasColumnName("version")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(model => model.ModelType)
            .HasColumnName("model_type")
            .HasColumnType("system.ai_model_type")
            .IsRequired();

        builder.Property(model => model.ArtifactUri)
            .HasColumnName("artifact_uri")
            .HasColumnType("text");

        builder.Property(model => model.Metrics)
            .HasColumnName("metrics")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(model => model.TrainedAt)
            .HasColumnName("trained_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(model => model.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(model => model.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(model => model.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(model => new { model.ModelName, model.Version })
            .HasDatabaseName("uq_ai_model_versions_name_version")
            .IsUnique();
    }
}
