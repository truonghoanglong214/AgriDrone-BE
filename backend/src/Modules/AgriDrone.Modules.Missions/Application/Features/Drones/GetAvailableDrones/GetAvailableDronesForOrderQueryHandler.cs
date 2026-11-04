using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

internal sealed class GetAvailableDronesForOrderQueryHandler(
    ISurveyOrderMissionPlanningQuery orderPlanningQuery,
    ISystemManagerAccessService managerAccessService,
    IDroneQueries droneQueries)
    : IRequestHandler<GetAvailableDronesForOrderQuery,
        Result<AvailableDronesForOrderResponse>>
{
    public async Task<Result<AvailableDronesForOrderResponse>> Handle(
        GetAvailableDronesForOrderQuery request,
        CancellationToken cancellationToken)
    {
        SurveyOrderMissionPlanningContext? order;
        try
        {
            order = await orderPlanningQuery.GetAsync(
                request.SurveyOrderId, cancellationToken);
        }
        catch (SurveyOrderMissionPlanningUnavailableException)
        {
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.OrderContextUnavailable());
        }

        if (order is null)
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.OrderNotFound(request.SurveyOrderId));

        if (order.SurveyOrderId != request.SurveyOrderId ||
            order.TenantId == Guid.Empty || order.FarmId == Guid.Empty ||
            !Enum.IsDefined(order.SelectedService) ||
            order.AppointmentStartAt.Offset != TimeSpan.Zero ||
            order.AppointmentEndAt.Offset != TimeSpan.Zero ||
            order.AppointmentEndAt <= order.AppointmentStartAt)
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.InvalidOrderContext());

        var access = await managerAccessService.ResolveFarmAccessAsync(
            order.FarmId, cancellationToken);
        if (!access.IsAllowed)
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.FarmAccessDenied());
        if (access.TenantId != order.TenantId || access.FarmId != order.FarmId)
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.CrossTenantOrder());

        if (!order.IsEligibleForPlanning || !order.IsReadyToSchedule)
            return Result.Failure<AvailableDronesForOrderResponse>(
                PrepareMissionSetErrors.OrderNotReady(order.ReadinessFailureCode));

        var purpose = order.RequiresBaselineMapping
            ? MissionPurpose.BaselineMapping
            : order.SelectedService switch
            {
                SurveyServiceType.PlantHealth => MissionPurpose.PlantHealth,
                SurveyServiceType.HarvestReadiness => MissionPurpose.HarvestReadiness,
                _ => throw new InvalidOperationException(
                    $"Unsupported Survey Service '{order.SelectedService}'.")
            };
        var available = await droneQueries.GetAvailableAsync(
            order.AppointmentStartAt,
            order.AppointmentEndAt,
            cancellationToken);
        var capable = available.Where(drone =>
            DroneCapabilityPolicy.Supports(drone.Specifications, purpose)).ToArray();

        return Result.Success(new AvailableDronesForOrderResponse(
            order.SurveyOrderId,
            order.FarmId,
            purpose.ToString(),
            order.AppointmentStartAt,
            order.AppointmentEndAt,
            capable));
    }
}
