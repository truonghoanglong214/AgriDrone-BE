using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Health;

public sealed record ComboAnalysisSourceContext(
    Guid TenantId,
    Guid FarmId,
    Guid? SurveyOrderId,
    Guid? CaptureAttemptId,
    Guid MissionId,
    Guid JobId,
    int SourceRevision,
    bool Completed,
    bool IsComboEligible,
    Guid? FarmBoundaryVersionId,
    Guid? FarmBaseMapVersionId,
    Guid? BaselineMapVersionId,
    Guid? MappingCandidateBatchId,
    MappingKind? MappingKind,
    Guid? BusinessPolicyVersionId,
    Guid? InputApprovalId,
    Guid? InputManifestId,
    string? InputManifestHash,
    Guid? SupersedesHandoffId,
    ComboAnalysisProvenanceV1? Provenance,
    string? LimitationNotice,
    IReadOnlyList<Guid> EvidenceMediaIds,
    IReadOnlyList<Guid>? InputMediaIds = null,
    Guid? ApprovedJobReference = null);

public interface IComboAnalysisSourceContextQuery
{
    Task<ComboAnalysisSourceContext?> GetAsync(
        Guid missionId,
        Guid jobId,
        CancellationToken cancellationToken);
}
