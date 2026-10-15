using AgriDrone.Modules.Plants.Domain.Guidance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class SourcedGuidanceConfiguration : IEntityTypeConfiguration<SourcedGuidance>
{
    public void Configure(EntityTypeBuilder<SourcedGuidance> builder)
    {
        builder.ToTable("sourced_guidance", "plant");
        builder.HasKey(item => item.Id).HasName("pk_sourced_guidance");
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.VersionNumber).HasColumnName("version_number");
        builder.Property(item => item.SuspectedClassCode).HasColumnName("suspected_class_code").HasMaxLength(80);
        builder.Property(item => item.SourceTitle).HasColumnName("source_title").HasMaxLength(300);
        builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(1000);
        builder.Property(item => item.GuidanceText).HasColumnName("guidance_text").HasMaxLength(4000);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(item => item.EffectiveTo).HasColumnName("effective_to");
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => new { item.Code, item.VersionNumber }).IsUnique().HasDatabaseName("uq_sourced_guidance_code_version");
        builder.HasIndex(item => new { item.SuspectedClassCode, item.Status, item.EffectiveFrom }).HasDatabaseName("ix_sourced_guidance_class_effective");
    }
}
