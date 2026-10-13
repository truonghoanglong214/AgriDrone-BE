using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public sealed record DiseaseZoneMembershipCandidate
{
    public DiseaseZoneMembershipCandidate(Guid plantId, decimal? confidence)
    {
        DomainGuard.NotEmpty(plantId);

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "Membership confidence must be between zero and one.");
        }

        PlantId = plantId;
        Confidence = confidence;
    }

    public Guid PlantId { get; }
    public decimal? Confidence { get; }
}
