using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest;

public sealed record SubmitNewFarmSurveyRequestCommand(
    string IdempotencyKey,
    Guid SurveyServiceId,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes)
    : IRequest<Result<SubmitNewFarmSurveyRequestResponse>>;
