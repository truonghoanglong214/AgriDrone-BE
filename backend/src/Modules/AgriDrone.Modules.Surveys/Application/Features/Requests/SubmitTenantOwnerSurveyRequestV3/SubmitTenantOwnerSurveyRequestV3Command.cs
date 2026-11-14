using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;

public sealed record SubmitTenantOwnerSurveyRequestV3Command(
    string IdempotencyKey,
    Guid SurveyServiceId,
    Guid? FarmId,
    string? FarmName,
    string? FarmAddress,
    decimal? ApproximateAreaHa,
    double? Longitude,
    double? Latitude,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes)
    : IRequest<Result<SubmitTenantOwnerSurveyRequestV3Response>>;
