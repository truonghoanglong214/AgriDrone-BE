using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class BusinessPolicyVersionConfiguration : IEntityTypeConfiguration<BusinessPolicyVersion>
{
    public void Configure(EntityTypeBuilder<BusinessPolicyVersion> builder)
    {
        builder.ToTable(
            "business_policy_versions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_business_policy_versions_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_business_policy_versions_version",
                    "version_number >= 1");
                table.HasCheckConstraint(
                    "ck_business_policy_versions_currency",
                    "pricing_currency ~ '^[A-Z]{3}$'");
            });

        builder.HasKey(version => version.Id).HasName("pk_business_policy_versions");
        builder.Property(version => version.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(version => version.StreamKey).HasColumnName("stream_key").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(version => version.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
        builder.Property(version => version.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(version => version.SurveyDepositPolicyId).HasColumnName("survey_deposit_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(version => version.ClaimPolicyId).HasColumnName("claim_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(version => version.CancellationPolicyId).HasColumnName("cancellation_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(version => version.ServiceRevisionIds)
            .HasColumnName("service_revision_ids")
            .HasColumnType("uuid[]")
            .HasDefaultValueSql("ARRAY[]::uuid[]")
            .IsRequired();
        builder.Property(version => version.PricingCurrency).HasColumnName("pricing_currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(version => version.PolicyFingerprint).HasColumnName("policy_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(version => version.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(version => version.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(version => version.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(version => version.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(version => version.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(version => version.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(version => new { version.StreamKey, version.VersionNumber })
            .HasDatabaseName("uq_business_policy_versions_stream_version")
            .IsUnique();

        builder.HasIndex(version => new { version.StreamKey, version.EffectiveFrom })
            .HasDatabaseName("ix_business_policy_versions_effective")
            .IsDescending(false, true);

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(version => version.PredecessorVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_versions_predecessor");

        builder.HasOne<SurveyDepositPolicy>()
            .WithMany()
            .HasForeignKey(version => version.SurveyDepositPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_versions_deposit_policy");

        builder.HasOne<ClaimPolicy>()
            .WithMany()
            .HasForeignKey(version => version.ClaimPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_versions_claim_policy");

        builder.HasOne<CancellationPolicy>()
            .WithMany()
            .HasForeignKey(version => version.CancellationPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_business_policy_versions_cancellation_policy");
    }
}
