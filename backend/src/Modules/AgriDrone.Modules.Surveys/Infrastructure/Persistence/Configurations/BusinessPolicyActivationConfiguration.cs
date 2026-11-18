using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class BusinessPolicyActivationConfiguration : IEntityTypeConfiguration<BusinessPolicyActivation>
{
    public void Configure(EntityTypeBuilder<BusinessPolicyActivation> builder)
    {
        builder.ToTable("business_policy_activation", "survey");

        builder.HasKey(activation => activation.Id).HasName("pk_business_policy_activation");
        builder.Property(activation => activation.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(activation => activation.ActiveBusinessPolicyVersionId).HasColumnName("active_business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(activation => activation.PublicIntakeEnabled).HasColumnName("public_intake_enabled").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(activation => activation.CaptureEnabled).HasColumnName("capture_enabled").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(activation => activation.ReleaseEnabled).HasColumnName("release_enabled").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(activation => activation.LastDecisionId)
            .HasColumnName("last_decision_id")
            .HasColumnType("uuid")
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(activation => activation.ActivatedAt).HasColumnName("activated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(activation => activation.ActivatedBy).HasColumnName("activated_by").HasColumnType("uuid").IsRequired();
        builder.Property(activation => activation.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(activation => activation.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(activation => activation.ActiveBusinessPolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_activation_policy_version");
    }
}
