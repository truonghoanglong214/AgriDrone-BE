using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal interface ISurveyRequestQueries
{
    Task<PagedResult<SurveyRequestInboxItem>> GetSystemAdminInboxAsync(
        SurveyRequestInboxFilter filter,
        PagedRequest page,
        CancellationToken cancellationToken = default);

    Task<SurveyRequestDetail?> GetSystemAdminDetailAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SurveyRequestHistoryItem>> GetTenantOwnerHistoryAsync(
        Guid tenantId,
        PagedRequest page,
        CancellationToken cancellationToken = default);
}

internal sealed record SurveyRequestInboxFilter(
    SurveyRequestStatus? Status = null,
    SurveyRequestKind? Kind = null,
    Guid? SurveyServiceId = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null);

internal sealed record SurveyRequestInboxItem(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    Guid? TenantId,
    Guid? FarmId,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string FarmName,
    decimal ApproximateAreaHa,
    int? EstimatedPoleCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

internal sealed record SurveyRequestDetail(
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
    double Longitude,
    double Latitude,
    int MapSrid,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SurveyRequestReviewItem> Reviews);

internal sealed record SurveyRequestReviewItem(
    Guid Id,
    SurveyReviewDecision Decision,
    JsonDocument ChecklistSnapshot,
    string Reason,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt);

internal sealed record SurveyRequestHistoryItem(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    Guid SurveyServiceId,
    string SurveyServiceCode,
    Guid? FarmId,
    string FarmName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
