using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class HarvestReadinessCriterionConfiguration
    : IEntityTypeConfiguration<HarvestReadinessCriterion>
{
    public void Configure(EntityTypeBuilder<HarvestReadinessCriterion> builder)
    {
        builder.ToTable(
            "harvest_readiness_criteria",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criteria_version",
                    "version_number >= 1");
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criteria_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criteria_lineage",
                    "(version_number = 1 AND supersedes_criterion_id IS NULL) OR " +
                    "(version_number > 1 AND supersedes_criterion_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criteria_validation",
                    "(status = 'VALIDATED'::system.harvest_readiness_criterion_status " +
                    "AND validated_by IS NOT NULL AND validated_at IS NOT NULL " +
                    "AND ground_truth_protocol IS NOT NULL " +
                    "AND dataset_requirements IS NOT NULL " +
                    "AND evaluation_protocol IS NOT NULL " +
                    "AND validation_evidence_reference IS NOT NULL) OR " +
                    "status <> 'VALIDATED'::system.harvest_readiness_criterion_status");
                table.HasCheckConstraint(
                    "ck_harvest_readiness_criteria_retirement",
                    "(status = 'RETIRED'::system.harvest_readiness_criterion_status " +
                    "AND retired_by IS NOT NULL AND retired_at IS NOT NULL) OR " +
                    "(status <> 'RETIRED'::system.harvest_readiness_criterion_status " +
                    "AND retired_by IS NULL AND retired_at IS NULL)");
            });

        builder.HasKey(criterion => criterion.Id)
            .HasName("pk_harvest_readiness_criteria");
        builder.HasAlternateKey(criterion => new
            {
                criterion.Id,
                criterion.VersionNumber
            })
            .HasName("ak_harvest_readiness_criteria_id_version");

        builder.Property(criterion => criterion.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(criterion => criterion.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(80)")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(criterion => criterion.VersionNumber)
            .HasColumnName("version_number")
            .HasColumnType("integer")
            .IsRequired();
        builder.Property(criterion => criterion.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(criterion => criterion.Description)
            .HasColumnName("description")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(criterion => criterion.Granularity)
            .HasColumnName("granularity")
            .HasColumnType("system.harvest_readiness_granularity")
            .IsRequired();
        builder.Property(criterion => criterion.Status)
            .HasColumnName("status")
            .HasColumnType("system.harvest_readiness_criterion_status")
            .HasSentinel((HarvestReadinessCriterionStatus)(-1))
            .HasDefaultValueSql(
                "'EXPERIMENTAL'::system.harvest_readiness_criterion_status")
            .IsRequired();
        builder.Property(criterion => criterion.ObservableIndicators)
            .HasColumnName("observable_indicators")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(criterion => criterion.GroundTruthProtocol)
            .HasColumnName("ground_truth_protocol")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(4000);
        builder.Property(criterion => criterion.DatasetRequirements)
            .HasColumnName("dataset_requirements")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(4000);
        builder.Property(criterion => criterion.EvaluationProtocol)
            .HasColumnName("evaluation_protocol")
            .HasColumnType("character varying(4000)")
            .HasMaxLength(4000);
        builder.Property(criterion => criterion.ValidationEvidenceReference)
            .HasColumnName("validation_evidence_reference")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000);
        builder.Property(criterion => criterion.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(criterion => criterion.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("timestamp with time zone");
        builder.Property(criterion => criterion.SupersedesCriterionId)
            .HasColumnName("supersedes_criterion_id")
            .HasColumnType("uuid");
        builder.Property(criterion => criterion.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(criterion => criterion.ValidatedBy)
            .HasColumnName("validated_by")
            .HasColumnType("uuid");
        builder.Property(criterion => criterion.ValidatedAt)
            .HasColumnName("validated_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(criterion => criterion.RetiredBy)
            .HasColumnName("retired_by")
            .HasColumnType("uuid");
        builder.Property(criterion => criterion.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(criterion => criterion.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
        builder.Property(criterion => criterion.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(criterion => criterion.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(criterion => new
            {
                criterion.Code,
                criterion.VersionNumber
            })
            .HasDatabaseName("uq_harvest_readiness_criteria_code_version")
            .IsUnique();
        builder.HasIndex(criterion => new
            {
                criterion.Code,
                criterion.Status,
                criterion.EffectiveFrom
            })
            .HasDatabaseName("ix_harvest_readiness_criteria_effective");

        builder.HasOne(criterion => criterion.SupersedesCriterion)
            .WithMany()
            .HasForeignKey(criterion => criterion.SupersedesCriterionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_harvest_readiness_criteria_supersedes_criterion_id");
    }
}
