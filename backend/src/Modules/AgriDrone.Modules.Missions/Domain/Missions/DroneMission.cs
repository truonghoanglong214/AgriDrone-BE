using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed class DroneMission : AggregateRoot
{
    private DroneMission()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid? ZoneId { get; private set; }

    public Guid DroneId { get; private set; }

    public Guid? PilotUserId { get; private set; }

    public string MissionCode { get; private set; } = null!;

    public MissionType MissionType { get; private set; }

    public MissionStatus Status { get; private set; }

    public ProcessingStatus ProcessingStatus { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public LineString? FlightRoute { get; private set; }

    public JsonDocument FlightParameters { get; private set; } = null!;

    public int? DetectedPlantCount { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? PublishedMapVersionId { get; private set; }

    public Guid? MappingApprovalId { get; private set; }

    public DateTimeOffset? MapPublishedAt { get; private set; }

    public Drone Drone { get; private set; } = null!;

    public ICollection<MissionMedia> Media { get; private set; } = [];

    public ICollection<MissionTelemetryPoint> TelemetryPoints { get; private set; } = [];

    public ICollection<AiProcessingJob> AiProcessingJobs { get; private set; } = [];

    public ICollection<MissionPlantObservation> PlantObservations { get; private set; } = [];

    public bool ApplyPublishedZoneMap(
        Guid approvalId,
        Guid mapVersionId,
        DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(approvalId);
        DomainGuard.NotEmpty(mapVersionId);
        DomainGuard.Utc(publishedAt);

        if (PublishedMapVersionId == mapVersionId &&
            MappingApprovalId == approvalId)
        {
            return false;
        }

        if (PublishedMapVersionId.HasValue || MappingApprovalId.HasValue)
        {
            throw new InvalidOperationException(
                "The mapping mission is already linked to a different published map.");
        }

        if (MissionType != MissionType.Mapping ||
            Status != MissionStatus.Completed ||
            ProcessingStatus != ProcessingStatus.ReviewRequired)
        {
            throw new InvalidOperationException(
                "Only a completed mapping flight awaiting review can accept a published map.");
        }

        PublishedMapVersionId = mapVersionId;
        MappingApprovalId = approvalId;
        MapPublishedAt = publishedAt;
        ProcessingStatus = ProcessingStatus.Completed;
        UpdatedAt = publishedAt;
        return true;
    }
}
