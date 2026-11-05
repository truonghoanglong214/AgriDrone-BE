using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

public sealed record GetAvailableDronesForOrderQuery(Guid SurveyOrderId)
    : IRequest<Result<AvailableDronesForOrderResponse>>;
