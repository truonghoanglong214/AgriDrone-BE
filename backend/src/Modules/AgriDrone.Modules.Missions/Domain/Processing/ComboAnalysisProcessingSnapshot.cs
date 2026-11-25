using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed record ComboAnalysisProcessingSnapshot(
    int SourceRevision,
    Guid CaptureAttemptId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid MappingCandidateBatchId,
    MappingKind MappingKind,
    Guid BusinessPolicyVersionId,
    Guid? InputApprovalId,
    Guid? InputManifestId,
    string? InputManifestHash,
    Guid? SupersedesHandoffId,
    string LimitationNotice,
    ComboAnalysisProvenanceV1 Provenance,
    Guid? ApprovedJobReference = null,
    IReadOnlyList<ProposedComboDiseaseZoneV1>? DiseaseZones = null);
