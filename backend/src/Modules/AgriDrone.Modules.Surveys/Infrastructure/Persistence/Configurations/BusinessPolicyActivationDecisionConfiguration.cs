using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class BusinessPolicyActivationDecisionConfiguration : IEntityTypeConfiguration<BusinessPolicyActivationDecision>
{
    public void Configure(EntityTypeBuilder<BusinessPolicyActivationDecision> builder)
    {
        builder.ToTable("business_policy_activation_decisions", "survey");

        builder.HasKey(decision => decision.Id).HasName("pk_business_policy_activation_decisions");
        builder.Property(decision => decision.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(decision => decision.ActivationId).HasColumnName("activation_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.BusinessPolicyVersionId).HasColumnName("business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.PublicIntakeEnabled).HasColumnName("public_intake_enabled").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.CaptureEnabled).HasColumnName("capture_enabled").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.ReleaseEnabled).HasColumnName("release_enabled").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.PredecessorDecisionId).HasColumnName("predecessor_decision_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(decision => decision.ChangedBy).HasColumnName("changed_by").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(decision => decision.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(decision => decision.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(decision => decision.ChangedAt)
            .HasDatabaseName("ix_business_policy_activation_decisions_changed_at")
            .IsDescending(true);

        builder.HasOne<BusinessPolicyActivation>()
            .WithMany()
            .HasForeignKey(decision => decision.ActivationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_activation_decisions_activation");

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(decision => decision.BusinessPolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_activation_decisions_policy_version");

        builder.HasOne<BusinessPolicyActivationDecision>()
            .WithMany()
            .HasForeignKey(decision => decision.PredecessorDecisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_activation_decisions_predecessor");
    }
}
