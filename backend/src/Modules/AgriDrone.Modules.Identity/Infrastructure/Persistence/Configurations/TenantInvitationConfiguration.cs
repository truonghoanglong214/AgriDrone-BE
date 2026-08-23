using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantInvitationConfiguration
    : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable(
            "tenant_invitations",
            "identity",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Single-use invitations for granting tenant membership through verified email ownership.");
                tableBuilder.HasCheckConstraint(
                    "ck_tenant_invitations_purpose_role",
                    "(purpose = 'OWNER_PROVISIONING'::system.tenant_invitation_purpose AND role = 'OWNER'::system.tenant_member_role) OR " +
                    "(purpose = 'MEMBERSHIP'::system.tenant_invitation_purpose AND role <> 'OWNER'::system.tenant_member_role)");
                tableBuilder.HasCheckConstraint(
                    "ck_tenant_invitations_expiration",
                    "expires_at > created_at");
            });

        builder.HasKey(invitation => invitation.Id)
            .HasName("pk_tenant_invitations");

        builder.Property(invitation => invitation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(invitation => invitation.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(invitation => invitation.Email)
            .HasColumnName("email")
            .HasColumnType("citext")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasColumnName("role")
            .HasColumnType("system.tenant_member_role")
            .IsRequired();

        builder.Property(invitation => invitation.Purpose)
            .HasColumnName("purpose")
            .HasColumnType("system.tenant_invitation_purpose")
            .HasDefaultValueSql(
                "'MEMBERSHIP'::system.tenant_invitation_purpose")
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("character(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(invitation => invitation.Status)
            .HasColumnName("status")
            .HasColumnType("system.tenant_invitation_status")
            .HasDefaultValueSql("'PENDING'::system.tenant_invitation_status")
            .IsRequired();

        builder.Property(invitation => invitation.InvitedByUserId)
            .HasColumnName("invited_by_user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedByUserId)
            .HasColumnName("accepted_by_user_id")
            .HasColumnType("uuid");

        builder.Property(invitation => invitation.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(invitation => invitation.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAt)
            .HasColumnName("accepted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(invitation => invitation.TokenHash)
            .HasDatabaseName("uq_tenant_invitations_token_hash")
            .IsUnique();

        builder.HasIndex(invitation => new
        {
            invitation.TenantId,
            invitation.Email
        })
            .HasDatabaseName("uq_tenant_invitations_pending_tenant_email")
            .HasFilter("status = 'PENDING'::system.tenant_invitation_status")
            .IsUnique();

        builder.HasIndex(invitation => invitation.TenantId)
            .HasDatabaseName(
                "uq_tenant_invitations_pending_owner_provisioning")
            .HasFilter(
                "purpose = 'OWNER_PROVISIONING'::system.tenant_invitation_purpose " +
                "AND status = 'PENDING'::system.tenant_invitation_status")
            .IsUnique();

        builder.HasIndex(invitation => new
        {
            invitation.Status,
            invitation.ExpiresAt
        }).HasDatabaseName("ix_tenant_invitations_status_expiration");

        builder.HasIndex(invitation => invitation.InvitedByUserId)
            .HasDatabaseName("ix_tenant_invitations_invited_by_user");

        builder.HasIndex(invitation => invitation.AcceptedByUserId)
            .HasDatabaseName("ix_tenant_invitations_accepted_by_user");

        builder.HasOne(invitation => invitation.Tenant)
            .WithMany()
            .HasForeignKey(invitation => invitation.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_invitations_tenants_tenant_id");

        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_invitations_users_invited_by_user_id");

        builder.HasOne(invitation => invitation.AcceptedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_invitations_users_accepted_by_user_id");
    }
}
