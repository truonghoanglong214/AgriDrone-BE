using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;

public sealed record SubmitTenantOwnerSurveyRequestV3Response(
    Guid Id,
    string RequestNumber,
    Guid TenantId,
    Guid? FarmId,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    SurveyRequestQuoteResponse IndicativeQuote,
    DateTimeOffset CreatedAt);
