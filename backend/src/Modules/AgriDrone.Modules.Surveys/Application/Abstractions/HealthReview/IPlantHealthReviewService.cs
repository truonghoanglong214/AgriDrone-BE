using System.Text.Json;
using System.Text.Json.Serialization;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.ApprovePlantHealthSurveyResult;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthReviewPoint(string Type, IReadOnlyList<double> Coordinates);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthReviewPolygon(string Type, IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates)
{
    public GeoJsonPolygonV3 ToGeoJson() => new(Type, Coordinates);
}

public interface IPlantHealthReviewService
{
    Task<Result<HealthReviewPage>> GetAsync(Guid orderId, string kind, HealthReviewQuery query, CancellationToken token);
    Task<Result<HealthReviewPage>> GetHistoryAsync(Guid orderId, Guid findingId, HealthReviewQuery query, CancellationToken token);
    Task<Result<IReadOnlyList<HealthGuidanceOption>>> GetGuidanceOptionsAsync(Guid orderId, Guid zoneId, CancellationToken token);
    Task<Result<HealthReviewSummary>> GetSummaryAsync(Guid orderId, CancellationToken token);
    Task<Result<HealthReviewOutcome>> ReviewFindingsAsync(Guid orderId, HealthFindingBatchRequest request, CancellationToken token);
    Task<Result<HealthReviewOutcome>> ReviewBoundaryAsync(Guid orderId, Guid exceptionId, HealthBoundaryReviewRequest request, CancellationToken token);
    Task<Result<HealthReviewOutcome>> ReviewZoneAsync(Guid orderId, Guid zoneId, HealthZoneReviewRequest request, CancellationToken token);
    Task<Result<HealthReviewOutcome>> ReviewGuidanceAsync(Guid orderId, Guid zoneId, HealthGuidanceReviewRequest request, CancellationToken token);
    Task<Result<ApprovePlantHealthSurveyResultResponse>> ApproveResultAsync(Guid orderId, ApprovePlantHealthSurveyResultRequest request, CancellationToken token);
    Task<Result<ApprovePlantHealthSurveyResultResponse>> GetApprovalAsync(Guid orderId, CancellationToken token);
}

public interface IPlantHealthReviewAuthorizationGuard
{
    Task<bool> AuthorizeAsync(Guid tenantId, Guid farmId, Guid actorId, CancellationToken token);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthReviewQuery(Guid? BatchId = null, int PageNumber = 1, int PageSize = 50,
    string? Decision = null, string? ClassCode = null, Guid? ZoneId = null, string? ResolutionState = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthFindingReviewItem(Guid FindingId, int ExpectedSourceVersion, uint? ExpectedReviewVersion,
    string Decision, string Reason, IReadOnlyList<Guid> EvidenceMediaIds, string? CorrectedClassCode = null,
    Guid? CorrectedPlantId = null, HealthReviewPoint? CorrectedPosition = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthFindingReviewRequest(Guid OperationId, Guid BatchId, uint ExpectedResultVersion,
    int ExpectedSourceVersion, uint? ExpectedReviewVersion, string Decision, string Reason,
    IReadOnlyList<Guid> EvidenceMediaIds, string? CorrectedClassCode = null, Guid? CorrectedPlantId = null,
    HealthReviewPoint? CorrectedPosition = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthFindingBatchRequest(Guid OperationId, Guid BatchId, uint ExpectedResultVersion,
    IReadOnlyList<HealthFindingReviewItem> Items);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthBoundaryReviewRequest(Guid OperationId, Guid BatchId, uint ExpectedResultVersion,
    uint ExpectedExceptionVersion, uint? ExpectedReviewVersion, string Decision, string Reason,
    IReadOnlyList<Guid> EvidenceMediaIds, Guid? FindingId = null, Guid? FindingReviewId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthZoneMember(Guid FindingId, int SourceVersion, Guid FindingReviewId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthZoneReviewRequest(Guid OperationId, Guid BatchId, uint ExpectedResultVersion,
    uint ExpectedZoneVersion, uint? ExpectedReviewVersion, string Decision, string Reason,
    IReadOnlyList<Guid> EvidenceMediaIds, HealthReviewPolygon? CorrectedGeometry = null,
    string? CorrectedClassCode = null, IReadOnlyList<HealthZoneMember>? Members = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HealthGuidanceReviewRequest(Guid OperationId, Guid BatchId, uint ExpectedResultVersion,
    uint ExpectedZoneVersion, uint? ExpectedReviewVersion, Guid ZoneReviewId, string Decision,
    string Reason, IReadOnlyList<Guid> EvidenceMediaIds, Guid? GuidanceId = null);

public sealed record HealthReviewStateView(Guid ReviewId, uint? Version, string Decision,
    Guid ReviewedBy, DateTimeOffset ReviewedAt, string Reason, JsonElement Snapshot);
public sealed record HealthReviewItemView(Guid Id, int? SourceVersion, uint SourceRowVersion,
    DateTimeOffset ObservedAt, string ResolutionState, JsonElement Original,
    HealthReviewStateView? Review, IReadOnlyList<string> BlockingReasons, HealthReviewStateView? GuidanceReview = null);
public sealed record HealthReviewPage(Guid ResultId, Guid BatchId, uint ResultVersion, long ReviewRevision,
    string ResultNature, string TeamLabelLimitation, bool IsCurrentBatch,
    int PageNumber, int PageSize, int TotalCount, IReadOnlyList<HealthReviewItemView> Items);
public sealed record HealthGuidanceOption(Guid Id, string Code, int VersionNumber, string SuspectedClassCode,
    string SourceTitle, string SourceReference, string GuidanceText, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record HealthReviewSummary(Guid ResultId, Guid BatchId, uint ResultVersion, long ReviewRevision,
    int UnreviewedFindings, int AmbiguousFindings, int AcceptedFindings, int RejectedFindings, int NonDiseaseFindings,
    int UnresolvedBoundaryExceptions, int UndecidedZones, int RejectedZones, int ApprovedZones,
    int InvalidGuidance, int StaleDependencies, bool ReadyForApproval);
public sealed record HealthReviewedVersion(string Kind, Guid SubjectId, Guid ReviewId, uint Version, uint? SubjectRowVersion = null);
public sealed record HealthReviewOutcome(Guid OperationId, Guid ResultId, Guid BatchId, uint ResultVersion,
    long ReviewRevision, IReadOnlyList<HealthReviewedVersion> Reviews, IReadOnlyList<Guid> InvalidatedZoneIds,
    bool IsReplay = false, bool IsCurrentBatch = true);
