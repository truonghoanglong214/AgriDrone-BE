using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Observations;

public sealed class MissionPlantObservation : Entity
{
    private MissionPlantObservation()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid MissionId { get; private set; }

    public Guid? AiJobId { get; private set; }

    public Guid? ModelVersionId { get; private set; }

    public string? TrackingId { get; private set; }

    public Point? DetectedLocation { get; private set; }

    public decimal? DetectionConfidence { get; private set; }

    public Guid? SuggestedPlantId { get; private set; }

    public decimal? MatchConfidence { get; private set; }

    public Guid? ResolvedPlantId { get; private set; }

    public ObservationReviewStatus ReviewStatus { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public Guid? EvidenceMediaId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
