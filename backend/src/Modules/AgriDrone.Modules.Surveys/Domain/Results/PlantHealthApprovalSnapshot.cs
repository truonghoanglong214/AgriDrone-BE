using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record PlantHealthApprovedReview(Guid ReviewId, Guid SubjectId, string Kind, string Decision,
    uint HeadVersion, string SnapshotFingerprint);

/// <summary>The exact immutable human-review set approved for later publication.</summary>
public sealed record PlantHealthApprovalSnapshot(
    Guid OperationId, Guid ResultId, Guid SurveyOrderId, Guid TenantId, Guid FarmId,
    Guid BatchId, Guid MissionId, Guid HandoffId, Guid JobId,
    Guid FarmBoundaryVersionId, Guid FarmBaseMapVersionId, int SourceVersion, long ReviewRevision,
    string ResultNature, string TeamLabelLimitation, PlantHealthProvenanceV4 Provenance,
    string SourceFingerprint, IReadOnlyList<PlantHealthApprovedReview> Reviews,
    IReadOnlyList<Guid> EvidenceMediaIds, int AcceptedFindingCount, string Conclusion,
    Guid ApprovedBy, DateTimeOffset ApprovedAt, string Reason);
