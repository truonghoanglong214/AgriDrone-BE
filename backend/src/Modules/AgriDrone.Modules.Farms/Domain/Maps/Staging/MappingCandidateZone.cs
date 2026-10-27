using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps.Staging;

public sealed class MappingCandidateZone : Entity
{
    private readonly List<MappingCandidate> _candidates = [];

    private MappingCandidateZone() { }

    public Guid BatchId { get; private set; }
    public Guid ZoneId { get; private set; }
    public Guid? ExpectedCurrentZoneMapVersionId { get; private set; }
    public double GridBearingDeg { get; private set; }
    public double RowSpacingM { get; private set; }
    public double PlantSpacingM { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public MappingCandidateBatch Batch { get; private set; } = null!;
    public IReadOnlyCollection<MappingCandidate> Candidates => _candidates.AsReadOnly();

    public static MappingCandidateZone Create(
        Guid id,
        Guid batchId,
        Guid zoneId,
        Guid? expectedCurrentZoneMapVersionId,
        double gridBearingDeg,
        double rowSpacingM,
        double plantSpacingM,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(batchId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.Utc(createdAt);

        ArgumentOutOfRangeException.ThrowIfNegative(gridBearingDeg);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(gridBearingDeg, 360);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowSpacingM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plantSpacingM);

        return new MappingCandidateZone
        {
            Id = id,
            BatchId = batchId,
            ZoneId = zoneId,
            ExpectedCurrentZoneMapVersionId = expectedCurrentZoneMapVersionId,
            GridBearingDeg = gridBearingDeg,
            RowSpacingM = rowSpacingM,
            PlantSpacingM = plantSpacingM,
            CreatedAt = createdAt
        };
    }

    public void AddCandidate(MappingCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _candidates.Add(candidate);
    }
}
