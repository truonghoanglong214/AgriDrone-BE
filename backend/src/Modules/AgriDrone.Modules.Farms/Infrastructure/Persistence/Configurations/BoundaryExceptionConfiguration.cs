using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Farms.Infrastructure.Persistence.Configurations;

public sealed class BoundaryExceptionConfiguration : IEntityTypeConfiguration<BoundaryException>
{
    public void Configure(EntityTypeBuilder<BoundaryException> builder)
    {
        builder.ToTable(
            "boundary_exceptions",
            "farm",
            table =>
            {
                table.HasCheckConstraint("ck_boundary_exceptions_distance_nonnegative", "measured_distance_meters >= 0");
                table.HasCheckConstraint("ck_boundary_exceptions_threshold_positive", "threshold_meters > 0");
                table.HasCheckConstraint("ck_boundary_exceptions_original_position", "NOT ST_IsEmpty(original_position) AND ST_IsValid(original_position) AND ST_SRID(original_position) = 4326 AND GeometryType(original_position) = 'POINT'");
                table.HasCheckConstraint("ck_boundary_exceptions_corrected_position", "corrected_position IS NULL OR (NOT ST_IsEmpty(corrected_position) AND ST_IsValid(corrected_position) AND ST_SRID(corrected_position) = 4326 AND GeometryType(corrected_position) = 'POINT')");
                table.HasCheckConstraint(
                    "ck_boundary_exceptions_resolution",
                    "(state IN ('OUT_OF_BOUNDARY'::system.boundary_exception_state, 'NEEDS_REVIEW'::system.boundary_exception_state) AND decision IS NULL AND corrected_position IS NULL AND reviewed_by IS NULL AND reviewed_at IS NULL AND review_reason IS NULL AND review_evidence IS NULL) OR " +
                    "(state = 'RESOLVED'::system.boundary_exception_state AND decision IS NOT NULL AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND review_reason IS NOT NULL AND review_evidence IS NOT NULL AND ((decision = 'LOCATION_CORRECTED'::system.boundary_exception_decision AND corrected_position IS NOT NULL) OR (decision <> 'LOCATION_CORRECTED'::system.boundary_exception_decision AND corrected_position IS NULL)))");
            });

        builder.HasKey(boundaryException => boundaryException.Id).HasName("pk_boundary_exceptions");
        builder.Property(boundaryException => boundaryException.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(boundaryException => boundaryException.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.Source).HasColumnName("source").HasColumnType("system.boundary_exception_source").IsRequired();
        builder.Property(boundaryException => boundaryException.SourceReferenceId).HasColumnName("source_reference_id").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(boundaryException => boundaryException.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.MissionId).HasColumnName("mission_id").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.OriginalPosition).HasColumnName("original_position").HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(boundaryException => boundaryException.State).HasColumnName("state").HasColumnType("system.boundary_exception_state").IsRequired();
        builder.Property(boundaryException => boundaryException.MeasuredDistanceMeters).HasColumnName("measured_distance_meters").HasColumnType("numeric(12,3)").HasPrecision(12, 3);
        builder.Property(boundaryException => boundaryException.ThresholdMeters).HasColumnName("threshold_meters").HasColumnType("numeric(12,3)").HasPrecision(12, 3);
        builder.Property(boundaryException => boundaryException.PolicyVersion).HasColumnName("policy_version").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(boundaryException => boundaryException.Decision).HasColumnName("decision").HasColumnType("system.boundary_exception_decision");
        builder.Property(boundaryException => boundaryException.CorrectedPosition).HasColumnName("corrected_position").HasColumnType("geometry(Point,4326)");
        builder.Property(boundaryException => boundaryException.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.Property(boundaryException => boundaryException.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone");
        builder.Property(boundaryException => boundaryException.ReviewReason).HasColumnName("review_reason").HasColumnType("character varying(2000)").HasMaxLength(2000);
        builder.Property(boundaryException => boundaryException.ReviewEvidence).HasColumnName("review_evidence").HasColumnType("jsonb");
        builder.Property(boundaryException => boundaryException.Version).IsRowVersion();
        builder.Property(boundaryException => boundaryException.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(boundaryException => boundaryException.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();

        builder.HasIndex(boundaryException => new { boundaryException.FarmBoundaryVersionId, boundaryException.Source, boundaryException.SourceReferenceId }).HasDatabaseName("uq_boundary_exceptions_unresolved_source").HasFilter("state <> 'RESOLVED'::system.boundary_exception_state").IsUnique();
        builder.HasIndex(boundaryException => new { boundaryException.FarmId, boundaryException.State, boundaryException.CreatedAt }).HasDatabaseName("ix_boundary_exceptions_review_queue");
        builder.HasIndex(boundaryException => boundaryException.OriginalPosition).HasDatabaseName("ix_boundary_exceptions_original_position_gist").HasMethod("gist");
        builder.HasIndex(boundaryException => boundaryException.CorrectedPosition).HasDatabaseName("ix_boundary_exceptions_corrected_position_gist").HasMethod("gist");

        builder.HasOne<FarmBoundary>().WithMany().HasForeignKey(boundaryException => new { boundaryException.FarmBoundaryVersionId, boundaryException.TenantId, boundaryException.FarmId }).HasPrincipalKey(boundary => new { boundary.Id, boundary.TenantId, boundary.FarmId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_boundary_exceptions_boundary_same_tenant_farm");
        builder.HasOne<Farm>().WithMany().HasForeignKey(boundaryException => new { boundaryException.FarmId, boundaryException.TenantId }).HasPrincipalKey(farm => new { farm.Id, farm.TenantId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_boundary_exceptions_farms_same_tenant");
    }
}
