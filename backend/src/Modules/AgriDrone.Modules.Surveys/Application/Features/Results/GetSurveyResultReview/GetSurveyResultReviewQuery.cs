using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;

public sealed record GetSurveyResultReviewQuery(Guid OrderId)
    : IRequest<Result<SurveyResultReviewResponse>>;

public sealed record SurveyResultReviewResponse(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    string MappingKind,
    Guid CaptureAttemptId,
    uint CaptureAttemptVersion,
    string CaptureAttemptStatus,
    Guid MissionId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    Guid MappingCandidateBatchId,
    Guid VerifiedPoleCountSnapshotId,
    int VerifiedBillablePoleCount,
    string CountSnapshotFingerprint,
    int CountSnapshotRevision,
    Guid? CurrentApprovedResultRevisionId,
    SurveyComboPendingReviewBatch? ComboPendingBatch,
    SurveyComboReviewSummaryDto? CurrentComboReview,
    SurveyExpectedFreezeTokensDto ExpectedFreezeTokens,
    string? CurrentApprovedResultFingerprint = null,
    IReadOnlyList<ComboSnapTargetDto>? ComboSnapTargets = null);
