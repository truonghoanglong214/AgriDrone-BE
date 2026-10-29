namespace AgriDrone.Modules.Farms.Application.Services;

public sealed record BaselineMappingBatchValidationResult(
    bool IsValid,
    int TotalCandidates,
    int AcceptedCount,
    int RejectedCount,
    int RelocatedCount,
    int ReplacedCount,
    int PendingCount,
    int ConfirmedPublishablePoleCount,
    IReadOnlyList<MappingBatchValidationIssue> Issues);
