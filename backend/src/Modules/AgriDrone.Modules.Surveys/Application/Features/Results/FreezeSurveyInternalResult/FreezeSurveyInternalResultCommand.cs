using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;

public sealed record FreezeSurveyInternalResultCommand(
    Guid OrderId,
    Guid OperationId,
    uint ExpectedOrderVersion,
    Guid ExpectedAttemptId,
    Guid ExpectedCountSnapshotId,
    string ExpectedCountFingerprint,
    string ReviewDecision,
    string FreezeReason,
    Guid? ExpectedComboBatchId = null,
    int? ExpectedComboSourceRevision = null,
    string? ExpectedComboBatchFingerprint = null,
    Guid? ExpectedComboReviewRevisionId = null,
    string? ExpectedComboReviewFingerprint = null)
    : IRequest<Result<FreezeSurveyInternalResultResponse>>;

public sealed record FreezeSurveyInternalResultResponse(
    Guid OrderId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    Guid ApprovedResultRevisionId,
    int ApprovedResultRevisionNumber,
    string ApprovedResultFingerprint,
    string ReviewDecision,
    string HealthAssessmentStatus,
    string? LimitationNotice,
    Guid AttemptId,
    uint AttemptVersion,
    string AttemptStatus,
    Guid MissionId,
    Guid CountSnapshotId,
    int VerifiedBillablePoleCount,
    string CountSnapshotFingerprint,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    Guid MappingCandidateBatchId,
    Guid? ComboBatchId,
    string? ComboBatchFingerprint,
    Guid? ComboReviewRevisionId,
    string? ComboReviewFingerprint,
    DateTimeOffset FrozenAt,
    bool IsReplay = false);
