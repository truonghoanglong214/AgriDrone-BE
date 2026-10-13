using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record PrepareMissionSetCommand(
    Guid SurveyOrderId,
    Guid DroneId,
    Guid OperationId,
    MissionScheduleWindow? ServiceWindow,
    MissionScheduleWindow? BaselineWindow)
    : IRequest<Result<PrepareMissionSetResult>>;
