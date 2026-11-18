using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;

public sealed record SubmitNewCustomerSurveyRequestV3Response(
    Guid Id,
    string RequestNumber,
    string TenantCode,
    string TenantName,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    string TrackingToken,
    SurveyRequestQuoteResponse IndicativeQuote,
    DateTimeOffset CreatedAt);
