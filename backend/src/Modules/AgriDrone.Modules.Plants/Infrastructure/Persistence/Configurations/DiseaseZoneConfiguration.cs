using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;

public sealed class DiseaseZoneConfiguration : IEntityTypeConfiguration<DiseaseZone>
{
    public void Configure(EntityTypeBuilder<DiseaseZone> builder)
    {
        builder.ToTable(
            "disease_zones",
            "plant",
            table =>
            {
                table.HasCheckConstraint("ck_disease_zones_version_positive", "version_number >= 1");
                table.HasCheckConstraint("ck_disease_zones_membership_version_positive", "current_membership_version >= 1");
                table.HasCheckConstraint("ck_disease_zones_proposed_geometry", "NOT ST_IsEmpty(proposed_geometry) AND ST_IsValid(proposed_geometry) AND ST_SRID(proposed_geometry) = 4326 AND GeometryType(proposed_geometry) = 'POLYGON'");
                table.HasCheckConstraint("ck_disease_zones_reviewed_geometry", "reviewed_geometry IS NULL OR (NOT ST_IsEmpty(reviewed_geometry) AND ST_IsValid(reviewed_geometry) AND ST_SRID(reviewed_geometry) = 4326 AND GeometryType(reviewed_geometry) = 'POLYGON')");
                table.HasCheckConstraint(
                    "ck_disease_zones_revision",
                    "(version_number = 1 AND supersedes_disease_zone_id IS NULL) OR (version_number > 1 AND supersedes_disease_zone_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_disease_zones_lifecycle",
                    "(status = 'PROPOSED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'REVIEWED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'PUBLISHED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'REJECTED'::system.disease_zone_status AND reviewed_geometry IS NULL AND current_membership_version = 1 AND selected_treatment_recommendation_id IS NULL AND recommendations_rejected = FALSE AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NULL AND published_at IS NULL AND superseded_by_disease_zone_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'SUPERSEDED'::system.disease_zone_status AND reviewed_geometry IS NOT NULL AND current_membership_version > 1 AND ((selected_treatment_recommendation_id IS NOT NULL AND recommendations_rejected = FALSE) OR (selected_treatment_recommendation_id IS NULL AND recommendations_rejected = TRUE)) AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND published_by IS NOT NULL AND published_at IS NOT NULL AND superseded_by_disease_zone_id IS NOT NULL AND superseded_at IS NOT NULL)");
            });

        builder.HasKey(zone => zone.Id).HasName("pk_disease_zones");
        builder.HasAlternateKey(zone => new { zone.Id, zone.FarmId }).HasName("uq_disease_zones_id_farm");
        builder.HasAlternateKey(zone => new { zone.Id, zone.TenantId, zone.FarmId }).HasName("uq_disease_zones_id_tenant_farm");
        builder.Property(zone => zone.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(zone => zone.ZoneKey).HasColumnName("zone_key").HasColumnType("uuid");
        builder.Property(zone => zone.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(zone => zone.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(zone => zone.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(zone => zone.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(zone => zone.SurveyResultId).HasColumnName("survey_result_id").HasColumnType("uuid");
        builder.Property(zone => zone.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(zone => zone.FarmBaseMapVersionId).HasColumnName("farm_base_map_version_id").HasColumnType("uuid");
        builder.Property(zone => zone.PlantConditionId).HasColumnName("plant_condition_id").HasColumnType("uuid");
        builder.Property(zone => zone.HealthLevelId).HasColumnName("health_level_id").HasColumnType("uuid");
        builder.Property(zone => zone.SourceHandoffId).HasColumnName("source_handoff_id").HasColumnType("uuid");
        builder.Property(zone => zone.SourceJobId).HasColumnName("source_job_id").HasColumnType("uuid");
        builder.Property(zone => zone.SourceProposalId).HasColumnName("source_proposal_id").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(zone => zone.ProposedGeometry).HasColumnName("proposed_geometry").HasColumnType("geometry(Polygon,4326)").IsRequired();
        builder.Property(zone => zone.ReviewedGeometry).HasColumnName("reviewed_geometry").HasColumnType("geometry(Polygon,4326)");
        builder.Property(zone => zone.RecommendationCandidates).HasColumnName("recommendation_candidates").HasColumnType("jsonb").IsRequired();
        builder.Property(zone => zone.ProposalEvidence).HasColumnName("proposal_evidence").HasColumnType("jsonb").IsRequired();
        builder.Property(zone => zone.CurrentMembershipVersion).HasColumnName("current_membership_version").HasColumnType("integer").HasDefaultValue(1).IsRequired();
        builder.Property(zone => zone.Status).HasColumnName("status").HasColumnType("system.disease_zone_status").HasSentinel((DiseaseZoneStatus)(-1)).HasDefaultValueSql("'PROPOSED'::system.disease_zone_status").IsRequired();
        builder.Property(zone => zone.SelectedTreatmentRecommendationId).HasColumnName("selected_treatment_recommendation_id").HasColumnType("uuid");
        builder.Property(zone => zone.RecommendationsRejected).HasColumnName("recommendations_rejected").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(zone => zone.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(zone => zone.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.Property(zone => zone.ReviewReason).HasColumnName("review_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(zone => zone.ReviewEvidence).HasColumnName("review_evidence").HasColumnType("jsonb");
        builder.Property(zone => zone.PublishedBy).HasColumnName("published_by").HasColumnType("uuid");
        builder.Property(zone => zone.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(zone => zone.SupersedesDiseaseZoneId).HasColumnName("supersedes_disease_zone_id").HasColumnType("uuid");
        builder.Property(zone => zone.SupersededByDiseaseZoneId).HasColumnName("superseded_by_disease_zone_id").HasColumnType("uuid");
        builder.Property(zone => zone.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(zone => zone.Version).IsRowVersion();
        builder.Property(zone => zone.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(zone => zone.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasMany(zone => zone.Memberships).WithOne().HasForeignKey(membership => membership.DiseaseZoneId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_disease_zone_memberships_disease_zones");
        builder.Navigation(zone => zone.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(zone => new { zone.ZoneKey, zone.VersionNumber }).HasDatabaseName("uq_disease_zones_key_version").IsUnique();
        builder.HasIndex(zone => zone.ZoneKey).HasDatabaseName("uq_disease_zones_one_published").HasFilter("status = 'PUBLISHED'::system.disease_zone_status").IsUnique();
        builder.HasIndex(zone => new { zone.SourceHandoffId, zone.SourceProposalId }).HasDatabaseName("uq_disease_zones_source_proposal").IsUnique();
        builder.HasIndex(zone => new { zone.FarmId, zone.Status, zone.CreatedAt }).HasDatabaseName("ix_disease_zones_review_queue");
        builder.HasIndex(zone => zone.ProposedGeometry).HasDatabaseName("ix_disease_zones_proposed_geometry_gist").HasMethod("gist");
        builder.HasIndex(zone => zone.ReviewedGeometry).HasDatabaseName("ix_disease_zones_reviewed_geometry_gist").HasMethod("gist");

        builder.HasOne<PlantCondition>().WithMany().HasForeignKey(zone => zone.PlantConditionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zones_plant_conditions");
        builder.HasOne<HealthLevel>().WithMany().HasForeignKey(zone => zone.HealthLevelId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zones_health_levels");
        builder.HasOne<TreatmentRecommendation>().WithMany().HasForeignKey(zone => zone.SelectedTreatmentRecommendationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zones_selected_recommendation");
        builder.HasOne<DiseaseZone>().WithMany().HasForeignKey(zone => new { zone.SupersedesDiseaseZoneId, zone.FarmId }).HasPrincipalKey(zone => new { zone.Id, zone.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zones_supersedes_same_farm");
        builder.HasOne<DiseaseZone>().WithMany().HasForeignKey(zone => new { zone.SupersededByDiseaseZoneId, zone.FarmId }).HasPrincipalKey(zone => new { zone.Id, zone.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_disease_zones_superseded_by_same_farm");
    }
}
