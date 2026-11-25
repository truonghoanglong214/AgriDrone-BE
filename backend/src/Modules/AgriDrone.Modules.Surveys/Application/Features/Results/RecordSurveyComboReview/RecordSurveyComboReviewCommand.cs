using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;

public sealed record ComboFindingReviewDecisionInput(
    Guid FindingId,
    string Decision,
    string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null);

public sealed record ComboZoneReviewDecisionInput(
    string SourceProposalId,
    string Decision,
    GeoJsonPolygonV3? ReviewedGeometry = null,
    IReadOnlyList<Guid>? ReviewedFindingIds = null,
    Guid? SelectedGuidanceId = null,
    int? SelectedGuidanceVersionNumber = null,
    string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null);

public sealed record ComboBlockerResolutionInput(
    string SourceType,
    string SourceId,
    string ReasonCode,
    string Resolution,
    string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null);

public sealed record RecordSurveyComboReviewCommand(
    Guid OrderId,
    Guid OperationId,
    uint ExpectedOrderVersion,
    Guid ExpectedBatchId,
    int ExpectedSourceRevision,
    string ExpectedBatchFingerprint,
    Guid? ExpectedPreviousReviewId,
    string Outcome,
    string ReviewReason,
    IReadOnlyList<ComboFindingReviewDecisionInput> FindingDecisions,
    IReadOnlyList<ComboZoneReviewDecisionInput> ZoneDecisions,
    IReadOnlyList<ComboBlockerResolutionInput> BlockerResolutions,
    bool ConfirmNoDiseaseObserved = false,
    string? NegativeResultConfirmationEvidence = null)
    : IRequest<Result<RecordSurveyComboReviewResponse>>;

public sealed record RecordSurveyComboReviewResponse(
    Guid OrderId,
    Guid BatchId,
    int SourceRevision,
    Guid ReviewRevisionId,
    int ReviewRevisionNumber,
    Guid? PredecessorReviewId,
    string DecisionFingerprint,
    string Outcome,
    int UnresolvedBlockersCount,
    IReadOnlyList<string> UnresolvedBlockers,
    DateTimeOffset ReviewedAt,
    bool IsReplay = false);
