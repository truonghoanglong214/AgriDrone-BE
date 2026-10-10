using System.Text.Json;

namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record SurveyRequestAcknowledgementApiResponse(
    Guid Id,
    string RequestNumber,
    string Kind,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record SurveyRequestInboxItemApiResponse(
    Guid Id,
    string RequestNumber,
    string Kind,
    string Status,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    Guid? TenantId,
    Guid? FarmId,
    string ApplicantName,
    string FarmName,
    decimal ApproximateAreaHa,
    int? EstimatedPoleCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record SurveyRequestDetailApiResponse(
    Guid Id,
    string RequestNumber,
    string Kind,
    string Status,
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
    SurveyRequestCandidateLocationApiResponse CandidateLocation,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ChecklistVersion,
    IReadOnlyList<SurveyRequestChecklistItemApiResponse> ChecklistItems,
    IReadOnlyList<SurveyRequestReviewApiResponse> Reviews);

public sealed record SurveyRequestCandidateLocationApiResponse(
    double Longitude,
    double Latitude,
    int MapSrid,
    bool IsApprovedFarmBoundary);

public sealed record SurveyRequestChecklistItemApiResponse(
    string Code,
    string Description);

public sealed record SurveyRequestReviewApiResponse(
    Guid Id,
    string Decision,
    JsonElement ChecklistSnapshot,
    string Reason,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt);

public sealed record SurveyRequestReviewMutationApiResponse(
    Guid Id,
    string RequestNumber,
    string Status,
    uint Version,
    DateTimeOffset UpdatedAt);

public sealed record SurveyRequestRejectionApiResponse(
    Guid Id,
    string RequestNumber,
    string Status,
    uint Version,
    Guid ReviewId,
    DateTimeOffset ReviewedAt);
