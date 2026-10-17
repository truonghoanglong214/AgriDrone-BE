using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyCaptureAuthorizationConfiguration
    : IEntityTypeConfiguration<SurveyCaptureAuthorization>
{
    public void Configure(EntityTypeBuilder<SurveyCaptureAuthorization> builder)
    {
        builder.ToTable("survey_capture_authorizations", "survey", table =>
        {
            table.HasCheckConstraint(
                "ck_capture_authorizations_verified_evidence",
                "(status <> 'VERIFIED'::system.survey_capture_authorization_status) OR " +
                "(evidence_reference IS NOT NULL AND issued_at IS NOT NULL AND verified_at IS NOT NULL AND verified_by IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_capture_authorizations_expiry",
                "expires_at IS NULL OR expires_at > verified_at");
            table.HasCheckConstraint(
                "ck_capture_authorizations_revocation",
                "(status <> 'REVOKED'::system.survey_capture_authorization_status) OR " +
                "(revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revocation_reason IS NOT NULL)");
        });
        builder.HasKey(item => item.Id).HasName("pk_survey_capture_authorizations");
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(item => item.Type).HasColumnName("authorization_type").HasColumnType("system.survey_capture_authorization_type");
        builder.Property(item => item.Status).HasColumnName("status").HasColumnType("system.survey_capture_authorization_status");
        builder.Property(item => item.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.VerifiedAt).HasColumnName("verified_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.VerifiedBy).HasColumnName("verified_by").HasColumnType("uuid");
        builder.Property(item => item.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.RevokedBy).HasColumnName("revoked_by").HasColumnType("uuid");
        builder.Property(item => item.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(2000);
        builder.Property(item => item.Version).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.SurveyOrderId, item.Type })
            .HasDatabaseName("uq_capture_authorizations_order_type")
            .IsUnique();
        builder.HasOne(item => item.SurveyOrder)
            .WithMany()
            .HasForeignKey(item => item.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_authorizations_orders_order_id");
    }
}
