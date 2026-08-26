using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable(
            "tenant_memberships",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "Tenant-level authorization and prerequisite membership for farm access."));

        builder.HasKey(membership => membership.Id).HasName("pk_tenant_memberships");
        builder.HasAlternateKey(membership => new
        {
            membership.TenantId,
            membership.UserId
        }).HasName("uq_tenant_memberships_tenant_user");

        builder.Property(membership => membership.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(membership => membership.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasColumnType("system.tenant_member_role")
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasColumnType("system.general_status")
            .HasSentinel((AgriDrone.SharedKernel.Domain.GeneralStatus)(-1))
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

        builder.Property(membership => membership.Version)
            .IsRowVersion();

        builder.HasIndex(membership => membership.UserId)
            .HasDatabaseName("ix_tenant_memberships_user");

        builder.HasIndex(membership => new
        {
            membership.TenantId,
            membership.Role,
            membership.Status
        }).HasDatabaseName("ix_tenant_memberships_tenant_role");

        builder.HasIndex(membership => membership.TenantId)
            .HasDatabaseName("uq_tenant_memberships_active_owner")
            .HasFilter(
                "role = 'OWNER'::system.tenant_member_role " +
                "AND status = 'ACTIVE'::system.general_status")
            .IsUnique();

        builder.HasOne(membership => membership.Tenant)
            .WithMany(tenant => tenant.Memberships)
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_memberships_tenants_tenant_id");

        builder.HasOne(membership => membership.User)
            .WithMany(user => user.TenantMemberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_memberships_users_user_id");
    }
}
