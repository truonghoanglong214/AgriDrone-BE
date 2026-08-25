using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(
            "users",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "System user accounts. If ASP.NET Core Identity is used, map/replace this table with AspNetUsers."));

        builder.HasKey(user => user.Id).HasName("pk_users");

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(user => user.FullName)
            .HasColumnName("full_name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(user => user.Phone)
            .HasColumnName("phone")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30);

        builder.Property(user => user.Status)
            .HasColumnName("status")
            .HasColumnType("system.user_status")
            .HasSentinel((UserStatus)(-1))
            .HasDefaultValueSql("'ACTIVE'::system.user_status")
            .IsRequired();

        builder.Property(user => user.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(user => user.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(user => user.Email)
            .HasDatabaseName("uq_users_email")
            .IsUnique();
    }
}
