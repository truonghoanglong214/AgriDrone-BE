using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable(
            "refresh_sessions",
            "identity",
            tableBuilder =>
            {
                tableBuilder.HasComment("Server-side refresh authentication sessions binding scope and auth version.");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_sessions_scope_pair",
                    "(tenant_id IS NULL AND tenant_membership_id IS NULL) OR (tenant_id IS NOT NULL AND tenant_membership_id IS NOT NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_sessions_absolute_expiration",
                    "absolute_expires_at > created_at");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_sessions_auth_version",
                    "authentication_version >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_sessions_revocation",
                    "(revoked_at IS NULL AND revocation_reason IS NULL) OR (revoked_at IS NOT NULL AND revocation_reason IS NOT NULL)");
            });

        builder.HasKey(session => session.Id).HasName("pk_refresh_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(session => session.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(session => session.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(session => session.TenantMembershipId)
            .HasColumnName("tenant_membership_id")
            .HasColumnType("uuid");

        builder.Property(session => session.AuthenticationVersion)
            .HasColumnName("authentication_version")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(session => session.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(session => session.AbsoluteExpiresAt)
            .HasColumnName("absolute_expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(session => session.LastRotatedAt)
            .HasColumnName("last_rotated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(session => session.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(session => session.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(session => session.Version)
            .IsRowVersion();

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("ix_refresh_sessions_user_active")
            .HasFilter("revoked_at IS NULL");

        builder.HasIndex(session => session.AbsoluteExpiresAt)
            .HasDatabaseName("ix_refresh_sessions_absolute_expires");

        builder.HasIndex(session => new { session.TenantMembershipId, session.TenantId, session.UserId })
            .HasDatabaseName("ix_refresh_sessions_membership");

        builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_sessions_user");

        builder.HasOne(session => session.TenantMembership)
            .WithMany()
            .HasForeignKey(session => new { session.TenantMembershipId, session.TenantId, session.UserId })
            .HasPrincipalKey(membership => new { membership.Id, membership.TenantId, membership.UserId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_refresh_sessions_membership");
    }
}
