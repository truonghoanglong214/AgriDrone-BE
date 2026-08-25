using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class AiThresholdProfileConfiguration : IEntityTypeConfiguration<AiThresholdProfile>
{
    public void Configure(EntityTypeBuilder<AiThresholdProfile> builder)
    {
        builder.ToTable(
            "ai_threshold_profiles",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Versioned threshold profile for one AI model; active versions are immutable.");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_threshold_profile_version_positive",
                    "version_number >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_threshold_profile_effective_time",
                    "effective_to IS NULL OR effective_from IS NULL OR effective_to >= effective_from");
            });

        builder.HasKey(profile => profile.Id).HasName("pk_ai_threshold_profiles");
        builder.HasAlternateKey(profile => new { profile.Id, profile.ModelVersionId })
            .HasName("uq_ai_threshold_profiles_id_model");

        builder.Property(profile => profile.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(profile => profile.ModelVersionId).HasColumnName("model_version_id").HasColumnType("uuid");
        builder.Property(profile => profile.ProfileName).HasColumnName("profile_name").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
builder.Property(profile => profile.Status).HasColumnName("status").HasColumnType("system.threshold_profile_status").HasSentinel((ThresholdProfileStatus)(-1)).HasDefaultValueSql("'DRAFT'::system.threshold_profile_status").IsRequired();
        builder.Property(profile => profile.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone");
        builder.Property(profile => profile.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(profile => profile.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(profile => profile.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(profile => new
        {
            profile.ModelVersionId,
            profile.ProfileName,
            profile.VersionNumber
        })
            .HasDatabaseName("uq_ai_threshold_profiles_model_name_version")
            .IsUnique();

        builder.HasOne(profile => profile.ModelVersion)
            .WithMany(model => model.ThresholdProfiles)
            .HasForeignKey(profile => profile.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_threshold_profiles_model_versions_model_id");
    }
}
