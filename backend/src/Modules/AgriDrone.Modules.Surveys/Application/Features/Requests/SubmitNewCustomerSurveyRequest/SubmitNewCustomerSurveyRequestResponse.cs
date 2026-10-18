using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;

public sealed record SubmitNewCustomerSurveyRequestResponse(
    Guid Id,
    string RequestNumber,
    string TenantCode,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
