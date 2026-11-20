namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record FarmBaseMapInternallyApprovedV1(
    Guid ApprovalId,
    Guid CausationId,
    MappingKind MappingKind,
    Guid SurveyOrderId,
    Guid SourceMissionId,
    Guid CaptureAttemptId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid MappingCandidateBatchId,
    Guid PoleCountSnapshotId,
    string EvidenceFingerprint,
    int VerifiedBillablePoleCount,
    Guid ApprovedBy,
    DateTimeOffset ApprovedAt);
