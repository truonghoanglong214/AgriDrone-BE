using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class SystemManagerInvitationConfiguration
    : IEntityTypeConfiguration<SystemManagerInvitation>
{
    public void Configure(
        EntityTypeBuilder<SystemManagerInvitation> builder)
    {
        builder.ToTable(
            "system_manager_invitations",
            "identity",
            table =>
            {
                table.HasComment(
                    "Single-use invitations for System Manager accounts.");

                table.HasCheckConstraint(
                    "ck_system_manager_invitations_expiration",
                    "expires_at > created_at");
            });

        builder.HasKey(invitation => invitation.Id)
            .HasName("pk_system_manager_invitations");

        builder.Property(invitation => invitation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.Email)
            .HasColumnName("email")
            .HasColumnType("citext")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("character(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(invitation => invitation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
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
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAt)
            .HasColumnName("accepted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(invitation => invitation.TokenHash)
            .HasDatabaseName(
                "uq_system_manager_invitations_token_hash")
            .IsUnique();

        builder.HasIndex(invitation => invitation.Email)
            .HasDatabaseName(
                "uq_system_manager_invitations_pending_email")
            .HasFilter("status = 'Pending'")
            .IsUnique();

        builder.HasIndex(invitation => new
        {
            invitation.Status,
            invitation.ExpiresAt
        })
            .HasDatabaseName(
                "ix_system_manager_invitations_status_expiration");

        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_system_manager_invitations_invited_by_user");

        builder.HasOne(invitation => invitation.AcceptedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_system_manager_invitations_accepted_by_user");
    }
}