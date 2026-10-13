using AgriDrone.Modules.Identity.Domain.SystemManagers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class SystemManagerProfileConfiguration
    : IEntityTypeConfiguration<SystemManagerProfile>
{
    public void Configure(EntityTypeBuilder<SystemManagerProfile> builder)
    {
        builder.ToTable(
            "system_manager_profiles",
            "identity",
            table => table.HasComment(
                "Operational profiles for AgriDrone SystemManagers; independent of customer tenant membership."));

        builder.HasKey(profile => profile.Id)
            .HasName("pk_system_manager_profiles");

        builder.Property(profile => profile.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(profile => profile.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(profile => profile.Status)
            .HasColumnName("status")
            .HasColumnType("system.system_manager_profile_status")
            .IsRequired();

        builder.Property(profile => profile.Availability)
            .HasColumnName("availability")
            .HasColumnType("system.system_manager_availability_status")
            .IsRequired();

        builder.Property(profile => profile.QualificationStatus)
            .HasColumnName("qualification_status")
            .HasColumnType("system.flight_qualification_status")
            .IsRequired();

        builder.Property(profile => profile.QualificationExpiresAt)
            .HasColumnName("qualification_expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(profile => profile.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(profile => profile.Version)
            .HasColumnName("version")
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(profile => profile.UserId)
            .HasDatabaseName("uq_system_manager_profiles_user")
            .IsUnique();

        builder.HasIndex(profile => new
        {
            profile.Status,
            profile.Availability,
            profile.QualificationStatus,
            profile.QualificationExpiresAt
        })
            .HasDatabaseName("ix_system_manager_profiles_assignability");

        builder.HasOne(profile => profile.User)
            .WithOne()
            .HasForeignKey<SystemManagerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_system_manager_profiles_users_user_id");
    }
}
