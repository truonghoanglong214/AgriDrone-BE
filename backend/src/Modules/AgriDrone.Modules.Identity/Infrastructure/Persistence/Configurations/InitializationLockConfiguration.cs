using AgriDrone.Modules.Identity.Infrastructure.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class InitializationLockConfiguration
    : IEntityTypeConfiguration<InitializationLock>
{
    public void Configure(EntityTypeBuilder<InitializationLock> builder)
    {
        builder.ToTable(
            "initialization_locks",
            "identity",
            tableBuilder => tableBuilder.HasComment(
                "Singleton rows used to serialize distributed initialization operations."));

        builder.HasKey(initializationLock => initializationLock.Name)
            .HasName("pk_initialization_locks");

        builder.Property(initializationLock => initializationLock.Name)
            .HasColumnName("name")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(initializationLock => initializationLock.Version)
            .HasColumnName("version")
            .HasColumnType("bigint");

        builder.HasData(new
        {
            Name = InitializationLock.SystemAdminBootstrapName,
            Version = 0L
        });
    }
}
