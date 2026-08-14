using AgriDrone.Modules.Identity.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable(
            "tenants",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "Top-level data isolation boundary owning farms and tenant-scoped resources."));

        builder.HasKey(tenant => tenant.Id).HasName("pk_tenants");

        builder.Property(tenant => tenant.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(tenant => tenant.Code)
            .HasColumnName("code")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(tenant => tenant.Status)
            .HasColumnName("status")
            .HasColumnType("system.general_status")
            .HasDefaultValueSql("'ACTIVE'::system.general_status")
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(tenant => tenant.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(tenant => tenant.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(tenant => tenant.Code)
            .HasDatabaseName("ux_tenants_code_active")
            .HasFilter("deleted_at IS NULL")
            .IsUnique();
    }
}
