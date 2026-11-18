using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyServiceRevisionConfiguration : IEntityTypeConfiguration<SurveyServiceRevision>
{
    public void Configure(EntityTypeBuilder<SurveyServiceRevision> builder)
    {
        builder.ToTable(
            "survey_service_revisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_service_revisions_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_survey_service_revisions_capabilities",
                    "maps_poles OR screens_disease");
                table.HasCheckConstraint(
                    "ck_survey_service_revisions_version",
                    "version_number >= 1");
            });

        builder.HasKey(revision => revision.Id).HasName("pk_survey_service_revisions");
        builder.Property(revision => revision.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(revision => revision.SurveyServiceId).HasColumnName("survey_service_id").HasColumnType("uuid").IsRequired();
        builder.Property(revision => revision.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
        builder.Property(revision => revision.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(revision => revision.Name).HasColumnName("name").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(revision => revision.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(revision => revision.MapsPoles).HasColumnName("maps_poles").HasColumnType("boolean").IsRequired();
        builder.Property(revision => revision.ScreensDisease).HasColumnName("screens_disease").HasColumnType("boolean").IsRequired();
        builder.Property(revision => revision.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(revision => revision.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(revision => revision.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(revision => revision.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(revision => revision.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(revision => revision.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(revision => new { revision.SurveyServiceId, revision.VersionNumber })
            .HasDatabaseName("uq_survey_service_revisions_service_version")
            .IsUnique();

        builder.HasIndex(revision => new { revision.SurveyServiceId, revision.EffectiveFrom })
            .HasDatabaseName("ix_survey_service_revisions_effective")
            .IsDescending(false, true);

        builder.HasOne<SurveyServiceRevision>()
            .WithMany()
            .HasForeignKey(revision => revision.PredecessorVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_service_revisions_predecessor");
    }
}
