using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;

public sealed record SubmitNewCustomerSurveyRequestResponse(
    Guid Id,
    string RequestNumber,
    string TenantCode,
    string TenantName,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
