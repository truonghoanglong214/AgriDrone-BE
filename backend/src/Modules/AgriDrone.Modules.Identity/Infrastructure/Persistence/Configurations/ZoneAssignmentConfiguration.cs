using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Domain.ZoneAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class ZoneAssignmentConfiguration : IEntityTypeConfiguration<ZoneAssignment>
{
    public void Configure(EntityTypeBuilder<ZoneAssignment> builder)
    {
        builder.ToTable(
            "zone_assignments",
            "identity",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Zone access granted to a farm membership with SELECTED_ZONES scope.");
                tableBuilder.HasCheckConstraint(
                    "ck_zone_assignments_revoked_after_assigned",
                    "revoked_at IS NULL OR revoked_at >= assigned_at");
            });

        builder.HasKey(assignment => assignment.Id).HasName("pk_zone_assignments");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.FarmMembershipId)
            .HasColumnName("farm_membership_id")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.ZoneId)
            .HasColumnName("zone_id")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.AssignedBy)
            .HasColumnName("assigned_by")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(assignment => assignment.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(assignment => new
        {
            assignment.FarmMembershipId,
            assignment.ZoneId
        })
            .HasDatabaseName("ux_zone_assignments_membership_zone_active")
            .HasFilter("revoked_at IS NULL")
            .IsUnique();

        builder.HasIndex(assignment => new { assignment.FarmId, assignment.ZoneId })
            .HasDatabaseName("ix_zone_assignments_farm_zone");

        builder.HasOne(assignment => assignment.FarmMembership)
            .WithMany(membership => membership.ZoneAssignments)
            .HasForeignKey(assignment => new
            {
                assignment.FarmMembershipId,
                assignment.FarmId
            })
            .HasPrincipalKey(membership => new { membership.Id, membership.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_zone_assignments_membership_same_farm");

        builder.HasOne(assignment => assignment.AssignedByUser)
            .WithMany(user => user.AssignedZoneAssignments)
            .HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_zone_assignments_users_assigned_by");
    }
}
