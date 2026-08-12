using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable(
            "user_roles",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "Many-to-many mapping between users and global roles."));

        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId })
            .HasName("pk_user_roles");

        builder.Property(userRole => userRole.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(userRole => userRole.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("uuid");

        builder.Property(userRole => userRole.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_roles_users_user_id");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_roles_roles_role_id");
    }
}
