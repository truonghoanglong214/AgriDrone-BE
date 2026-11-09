namespace AgriDrone.IntegrationContracts.Health;

/// <summary>Internal published-only proof; consumers do not query pending Surveys/Plants repositories.</summary>
public interface IPlantHealthPublicationQuery
{
    Task<PlantHealthPublishedProof?> GetAsync(Guid tenantId, Guid publicationId, CancellationToken token);
}

public sealed record PlantHealthPublishedProof(PlantHealthPreliminaryResultPublishedV1 Payload,
    string Fingerprint, int ReviewedFindingCount);
