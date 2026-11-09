using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed record PlantHealthProcessingSnapshot(
    int SourceVersion, Guid FarmBoundaryVersionId, Guid FarmBaseMapVersionId,
    PlantHealthProvenanceV4 Provenance, string TeamLabelLimitation);
