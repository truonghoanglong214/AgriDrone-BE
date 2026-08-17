using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration
    : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable(
            "password_reset_tokens",
            "identity",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Hashed, single-use tokens for resetting user passwords.");
                tableBuilder.HasCheckConstraint(
                    "ck_password_reset_tokens_expiration",
                    "expires_at > created_at");
            });

        builder.HasKey(token => token.Id)
            .HasName("pk_password_reset_tokens");

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("character(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(token => token.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(token => token.TokenHash)
            .HasDatabaseName("uq_password_reset_tokens_token_hash")
            .IsUnique();

        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("uq_password_reset_tokens_active_user")
            .HasFilter("used_at IS NULL AND revoked_at IS NULL")
            .IsUnique();

        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_password_reset_tokens_expiration");

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_password_reset_tokens_users_user_id");
    }
}
