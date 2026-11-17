using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Plants;

public sealed class PlantProfileObservation : Entity
{
    private PlantProfileObservation() { }

    public Guid PlantProfileId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid? MappingCandidateBatchId { get; private set; }
    public Guid? CandidateId { get; private set; }
    public Point ObservedLocation { get; private set; } = null!;
    public double ObservedDistanceM { get; private set; }
    public Guid ObservedBy { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }

    public static PlantProfileObservation Create(
        Guid id,
        Guid plantProfileId,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid farmBaseMapVersionId,
        Guid? mappingCandidateBatchId,
        Guid? candidateId,
        Point observedLocation,
        double observedDistanceM,
        DateTimeOffset observedAt,
        Guid observedBy) =>
        Record(
            id,
            plantProfileId,
            tenantId,
            farmId,
            surveyOrderId,
            farmBaseMapVersionId,
            observedLocation,
            observedDistanceM,
            candidateId,
            observedBy,
            observedAt,
            mappingCandidateBatchId);

    public static PlantProfileObservation Record(
        Guid id,
        Guid plantProfileId,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid farmBaseMapVersionId,
        Point observedLocation,
        double observedDistanceM,
        Guid? candidateId,
        Guid observedBy,
        DateTimeOffset observedAt,
        Guid? mappingCandidateBatchId = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(plantProfileId, nameof(plantProfileId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(farmBaseMapVersionId, nameof(farmBaseMapVersionId));
        DomainGuard.NotEmpty(observedBy, nameof(observedBy));
        DomainGuard.Utc(observedAt, nameof(observedAt));
        ArgumentNullException.ThrowIfNull(observedLocation, nameof(observedLocation));
        if (observedDistanceM < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observedDistanceM), "Observed distance cannot be negative.");
        }

        return new PlantProfileObservation
        {
            Id = id,
            PlantProfileId = plantProfileId,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            MappingCandidateBatchId = mappingCandidateBatchId,
            ObservedLocation = (Point)observedLocation.Copy(),
            ObservedDistanceM = observedDistanceM,
            CandidateId = candidateId,
            ObservedBy = observedBy,
            ObservedAt = observedAt
        };
    }
}
