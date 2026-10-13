using AgriDrone.Modules.Identity.Domain.SystemManagers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class FarmManagerAssignmentConfiguration
    : IEntityTypeConfiguration<FarmManagerAssignment>
{
    public void Configure(EntityTypeBuilder<FarmManagerAssignment> builder)
    {
        builder.ToTable(
            "farm_manager_assignments",
            "identity",
            table => table.HasComment(
                "Immutable primary SystemManager assignment history for each Farm."));

        builder.HasKey(assignment => assignment.Id)
            .HasName("pk_farm_manager_assignments");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(assignment => assignment.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(assignment => assignment.SystemManagerProfileId)
            .HasColumnName("system_manager_profile_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(assignment => assignment.AssignedBy)
            .HasColumnName("assigned_by")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(assignment => assignment.AssignmentReason)
            .HasColumnName("assignment_reason")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(assignment => assignment.EndedBy)
            .HasColumnName("ended_by")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.EndReason)
            .HasColumnName("end_reason")
            .HasColumnType("character varying(1000)")
            .HasMaxLength(1000);

        builder.Property(assignment => assignment.EndedAt)
            .HasColumnName("ended_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(assignment => assignment.Version)
            .HasColumnName("version")
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(assignment => assignment.FarmId)
            .HasDatabaseName("uq_farm_manager_assignments_active_farm")
            .HasFilter("ended_at IS NULL")
            .IsUnique();

        builder.HasIndex(assignment => new
        {
            assignment.SystemManagerProfileId,
            assignment.EndedAt
        })
            .HasDatabaseName("ix_farm_manager_assignments_profile_active");

        builder.HasIndex(assignment => new
        {
            assignment.TenantId,
            assignment.FarmId,
            assignment.AssignedAt
        })
            .HasDatabaseName("ix_farm_manager_assignments_history");

        builder.HasOne(assignment => assignment.SystemManagerProfile)
            .WithMany(profile => profile.FarmAssignments)
            .HasForeignKey(assignment => assignment.SystemManagerProfileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_profiles_profile_id");
    }
}
