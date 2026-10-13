using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;

public sealed record SurveyRequestDetailResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    Guid? TenantId,
    Guid? FarmId,
    Guid? RequestedByUserId,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    string SurveyServiceName,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    SurveyRequestCandidateLocationResponse CandidateLocation,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ChecklistVersion,
    IReadOnlyList<SurveyRequestReviewChecklistItem> ChecklistItems,
    IReadOnlyList<SurveyRequestReviewResponse> Reviews);

public sealed record SurveyRequestCandidateLocationResponse(
    double Longitude,
    double Latitude,
    int MapSrid,
    bool IsApprovedFarmBoundary = false);

public sealed record SurveyRequestReviewResponse(
    Guid Id,
    SurveyReviewDecision Decision,
    JsonElement ChecklistSnapshot,
    string Reason,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt);
