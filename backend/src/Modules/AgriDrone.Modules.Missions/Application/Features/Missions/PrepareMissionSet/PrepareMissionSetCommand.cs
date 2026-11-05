using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record PrepareMissionSetCommand(
    Guid SurveyOrderId,
    Guid DroneId,
    Guid IdempotencyKey)
    : IRequest<Result<PrepareMissionSetResult>>;
