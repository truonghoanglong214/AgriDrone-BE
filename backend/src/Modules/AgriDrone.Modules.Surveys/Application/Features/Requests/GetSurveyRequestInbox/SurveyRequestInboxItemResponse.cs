using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;

public sealed record SurveyRequestInboxItemResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
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
