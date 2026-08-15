using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class FarmMembershipConfiguration : IEntityTypeConfiguration<FarmMembership>
{
    public void Configure(EntityTypeBuilder<FarmMembership> builder)
    {
        builder.ToTable(
            "farm_memberships",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "Farm-level authorization for tenant members, optionally limited to selected zones."));

        builder.HasKey(membership => membership.Id).HasName("pk_farm_memberships");
        builder.HasAlternateKey(membership => new { membership.Id, membership.FarmId })
            .HasName("uq_farm_memberships_id_farm");

        builder.Property(membership => membership.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(membership => membership.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(membership => membership.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasColumnType("system.farm_member_role")
            .IsRequired();

        builder.Property(membership => membership.AccessScope)
            .HasColumnName("access_scope")
            .HasColumnType("system.farm_access_scope")
            .HasDefaultValueSql("'ALL_ZONES'::system.farm_access_scope")
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasColumnType("system.general_status")
            .HasDefaultValueSql("'ACTIVE'::system.general_status")
            .IsRequired();

        builder.Property(membership => membership.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(membership => membership.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(membership => new { membership.FarmId, membership.UserId })
            .HasDatabaseName("uq_farm_memberships_farm_user")
            .IsUnique();

        builder.HasIndex(membership => membership.UserId)
            .HasDatabaseName("ix_farm_memberships_user");

        builder.HasIndex(membership => new { membership.TenantId, membership.UserId })
            .HasDatabaseName("ix_farm_memberships_tenant_user");

        builder.HasIndex(membership => new
        {
            membership.FarmId,
            membership.Role,
            membership.Status
        })
            .HasDatabaseName("ix_farm_memberships_farm_role");

        builder.HasOne(membership => membership.User)
            .WithMany(user => user.FarmMemberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_farm_memberships_users_user_id");

        builder.HasOne(membership => membership.TenantMembership)
            .WithMany(tenantMembership => tenantMembership.FarmMemberships)
            .HasForeignKey(membership => new { membership.TenantId, membership.UserId })
            .HasPrincipalKey(tenantMembership => new
            {
                tenantMembership.TenantId,
                tenantMembership.UserId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_memberships_tenant_members_same_tenant");
    }
}
