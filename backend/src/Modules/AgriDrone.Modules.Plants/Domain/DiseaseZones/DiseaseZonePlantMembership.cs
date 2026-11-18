using AgriDrone.SharedKernel.Domain;
using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public sealed class DiseaseZonePlantMembership : Entity
{
    private DiseaseZonePlantMembership() { }

    internal static DiseaseZonePlantMembership Create(
        Guid diseaseZoneId,
        Guid farmId,
        DiseaseZoneMembershipCandidate candidate,
        int membershipVersion,
        DiseaseZoneMembershipKind kind,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(diseaseZoneId);
        DomainGuard.NotEmpty(farmId);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(membershipVersion);
        DomainGuard.Utc(createdAt);

        return new DiseaseZonePlantMembership
        {
            Id = Guid.NewGuid(),
            DiseaseZoneId = diseaseZoneId,
            FarmId = farmId,
            PlantId = candidate.PlantId,
            MembershipVersion = membershipVersion,
            Kind = kind,
            Confidence = candidate.Confidence,
            CreatedAt = createdAt
        };
    }

    public Guid DiseaseZoneId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? PlantId { get; private set; }
    public Guid? MappingCandidateId { get; private set; }
    public int MembershipVersion { get; private set; }
    public DiseaseZoneMembershipKind Kind { get; private set; }
    public decimal? Confidence { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static DiseaseZonePlantMembership CreatePending(Guid zoneId, Guid farmId,
        ProposedDiseaseZoneMembershipV4 source, int version, DateTimeOffset createdAt) => new()
    {
        Id = Guid.CreateVersion7(), DiseaseZoneId = zoneId, FarmId = farmId,
        PlantId = source.PlantId, MappingCandidateId = source.MappingCandidateId,
        MembershipVersion = version, Kind = DiseaseZoneMembershipKind.Proposed,
        Confidence = source.Confidence, CreatedAt = createdAt
    };
}
