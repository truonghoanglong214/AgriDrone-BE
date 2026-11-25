using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionTokenConfiguration : IEntityTypeConfiguration<RefreshSessionToken>
{
    public void Configure(EntityTypeBuilder<RefreshSessionToken> builder)
    {
        builder.ToTable(
            "refresh_session_tokens",
            "identity",
            tableBuilder =>
            {
                tableBuilder.HasComment("Append-only rotated refresh token hashes retained for reuse detection.");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_session_tokens_hash",
                    "token_hash ~ '^[0-9a-f]{64}$'");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_session_tokens_generation",
                    "(generation = 1 AND predecessor_token_id IS NULL) OR (generation > 1 AND predecessor_token_id IS NOT NULL AND predecessor_token_id <> id)");
                tableBuilder.HasCheckConstraint(
                    "ck_refresh_session_tokens_expiration",
                    "expires_at > issued_at");
            });

        builder.HasKey(token => token.Id).HasName("pk_refresh_session_tokens");
        builder.HasAlternateKey(token => new { token.Id, token.SessionId })
            .HasName("uq_refresh_session_tokens_id_session");

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(token => token.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(token => token.Generation)
            .HasColumnName("generation")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("character(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(token => token.PredecessorTokenId)
            .HasColumnName("predecessor_token_id")
            .HasColumnType("uuid");

        builder.Property(token => token.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(token => token.TokenHash)
            .HasDatabaseName("uq_refresh_session_tokens_hash")
            .IsUnique();

        builder.HasIndex(token => new { token.SessionId, token.Generation })
            .HasDatabaseName("uq_refresh_session_tokens_session_generation")
            .IsUnique();

        builder.HasIndex(token => token.SessionId)
            .HasDatabaseName("uq_refresh_session_tokens_one_unused_per_session")
            .HasFilter("used_at IS NULL")
            .IsUnique();

        builder.HasIndex(token => new { token.SessionId, token.PredecessorTokenId })
            .HasDatabaseName("uq_refresh_session_tokens_predecessor")
            .HasFilter("predecessor_token_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(token => new { token.PredecessorTokenId, token.SessionId })
            .HasDatabaseName("ix_refresh_session_tokens_predecessor_token");

        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_refresh_session_tokens_expires");

        builder.HasOne(token => token.Session)
            .WithMany(session => session.Tokens)
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_session_tokens_session");

        builder.HasOne(token => token.PredecessorToken)
            .WithMany()
            .HasForeignKey(token => new { token.PredecessorTokenId, token.SessionId })
            .HasPrincipalKey(predecessor => new { predecessor.Id, predecessor.SessionId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_refresh_session_tokens_predecessor_session");
    }
}
