using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class FarmBoundaryConfiguration : IEntityTypeConfiguration<FarmBoundary>
{
    public void Configure(EntityTypeBuilder<FarmBoundary> builder)
    {
        builder.ToTable(
            "farm_boundaries",
            "farm",
            table =>
            {
                table.HasCheckConstraint("ck_farm_boundaries_version_positive", "version_number >= 1");
                table.HasCheckConstraint(
                    "ck_farm_boundaries_geometry_valid",
                    "NOT ST_IsEmpty(geometry) AND ST_IsValid(geometry) AND ST_SRID(geometry) = 4326 AND GeometryType(geometry) = 'POLYGON'");
                table.HasCheckConstraint(
                    "ck_farm_boundaries_review_state",
                    "(status = 'DRAFT'::system.farm_boundary_status AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'APPROVED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'REJECTED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NULL AND superseded_at IS NULL) OR " +
                    "(status = 'SUPERSEDED'::system.farm_boundary_status AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND superseded_by_boundary_id IS NOT NULL AND superseded_at IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_farm_boundaries_legacy_source",
                    "source <> 'LEGACY_IMPORT'::system.farm_boundary_source OR source_survey_request_id IS NULL");
            });

        builder.HasKey(boundary => boundary.Id).HasName("pk_farm_boundaries");
        builder.HasAlternateKey(boundary => new { boundary.Id, boundary.FarmId })
            .HasName("uq_farm_boundaries_id_farm");
        builder.HasAlternateKey(boundary => new { boundary.Id, boundary.TenantId, boundary.FarmId })
            .HasName("uq_farm_boundaries_id_tenant_farm");

        builder.Property(boundary => boundary.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(boundary => boundary.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(boundary => boundary.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(boundary => boundary.VersionNumber).HasColumnName("version_number").HasColumnType("integer");
        builder.Property(boundary => boundary.Geometry).HasColumnName("geometry").HasColumnType("geometry(Polygon,4326)").IsRequired();
        builder.Property(boundary => boundary.Source).HasColumnName("source").HasColumnType("system.farm_boundary_source").IsRequired();
        builder.Property(boundary => boundary.SourceSurveyRequestId).HasColumnName("source_survey_request_id").HasColumnType("uuid");
        builder.Property(boundary => boundary.SubmittedBy).HasColumnName("submitted_by").HasColumnType("uuid");
        builder.Property(boundary => boundary.Status).HasColumnName("status").HasColumnType("system.farm_boundary_status").HasSentinel((FarmBoundaryStatus)(-1)).HasDefaultValueSql("'DRAFT'::system.farm_boundary_status").IsRequired();
        builder.Property(boundary => boundary.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(boundary => boundary.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.Property(boundary => boundary.ReviewReason).HasColumnName("review_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(boundary => boundary.SupersededByBoundaryId).HasColumnName("superseded_by_boundary_id").HasColumnType("uuid");
        builder.Property(boundary => boundary.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(boundary => boundary.Version).IsRowVersion();
        builder.Property(boundary => boundary.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(boundary => boundary.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(boundary => new { boundary.FarmId, boundary.VersionNumber }).HasDatabaseName("uq_farm_boundaries_farm_version").IsUnique();
        builder.HasIndex(boundary => boundary.FarmId).HasDatabaseName("uq_farm_boundaries_one_approved").HasFilter("status = 'APPROVED'::system.farm_boundary_status").IsUnique();
        builder.HasIndex(boundary => boundary.Geometry).HasDatabaseName("ix_farm_boundaries_geometry_gist").HasMethod("gist");

        builder.HasOne<Farm>().WithMany().HasForeignKey(boundary => new { boundary.FarmId, boundary.TenantId }).HasPrincipalKey(farm => new { farm.Id, farm.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_boundaries_farms_same_tenant");
        builder.HasOne<FarmBoundary>().WithMany().HasForeignKey(boundary => new { boundary.SupersededByBoundaryId, boundary.FarmId }).HasPrincipalKey(boundary => new { boundary.Id, boundary.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_farm_boundaries_replacement_same_farm");
    }
}
