using AgriDrone.Modules.Missions.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Database.Mapping;

internal sealed class MissionPublicationState
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid? ZoneId { get; private set; }

    public MissionType MissionType { get; private set; }

    public MissionStatus Status { get; private set; }

    public ProcessingStatus ProcessingStatus { get; private set; }
}

internal sealed class MissionPublicationStateConfiguration
    : IEntityTypeConfiguration<MissionPublicationState>
{
    public void Configure(
        EntityTypeBuilder<MissionPublicationState> builder)
    {
        builder.ToTable(
            "drone_missions",
            "mission",
            tableBuilder => tableBuilder.ExcludeFromMigrations());

        builder.HasKey(mission => mission.Id);

        builder.Property(mission => mission.Id)
            .HasColumnName("id");

        builder.Property(mission => mission.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(mission => mission.FarmId)
            .HasColumnName("farm_id");

        builder.Property(mission => mission.ZoneId)
            .HasColumnName("zone_id");

        builder.Property(mission => mission.MissionType)
            .HasColumnName("mission_type")
            .HasColumnType("system.mission_type");

        builder.Property(mission => mission.Status)
            .HasColumnName("status")
            .HasColumnType("system.mission_status");

        builder.Property(mission => mission.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasColumnType("system.processing_status");
    }
}
