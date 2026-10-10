using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest;

public sealed record SubmitExistingFarmSurveyRequestCommand(
    Guid FarmId,
    string IdempotencyKey,
    Guid SurveyServiceId,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes)
    : IRequest<Result<SubmitExistingFarmSurveyRequestResponse>>;
