using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

internal sealed class ChangeDroneStatusCommandHandler(
    IDroneRepository droneRepository,
    IDroneStatusChangeRepository statusChangeRepository,
    IMissionsUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : IRequestHandler<
        ChangeDroneStatusCommand,
        Result<ChangeDroneStatusResponse>>
{
    public async Task<Result<ChangeDroneStatusResponse>> Handle(
        ChangeDroneStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.CurrentUserRequired());
        }

        var drone = await droneRepository.GetByIdAsync(
            request.DroneId,
            request.TenantId,
            cancellationToken);

        if (drone is null)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.NotFound(request.DroneId));
        }

        if (drone.Status == request.TargetStatus)
        {
            return Result.Success(MapResponse(drone));
        }

        if (!CanTransition(
                drone.Status,
                request.TargetStatus))
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.InvalidStatusTransition(
                    drone.Status,
                    request.TargetStatus));
        }

        var previousStatus = drone.Status;
        var changedAt = DateTimeOffset.UtcNow;

        if (request.TargetStatus == DroneStatus.Available &&
            request.NextMaintenanceAt.HasValue &&
            request.NextMaintenanceAt.Value <= changedAt)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.InvalidNextMaintenanceTime());
        }

        ApplyTransition(
            drone,
            request.TargetStatus,
            changedAt,
            request.NextMaintenanceAt);

        var statusChange = DroneStatusChange.Create(
            drone.TenantId,
            drone.Id,
            previousStatus,
            drone.Status,
            userId,
            changedAt);

        statusChangeRepository.Add(statusChange);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MapResponse(drone));
    }

    private static bool CanTransition(
        DroneStatus currentStatus,
        DroneStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (DroneStatus.Available, DroneStatus.Maintenance) => true,
            (DroneStatus.Maintenance, DroneStatus.Available) => true,
            (DroneStatus.Available, DroneStatus.Retired) => true,
            (DroneStatus.Maintenance, DroneStatus.Retired) => true,
            _ => false
        };
    }

    private static void ApplyTransition(
        Drone drone,
        DroneStatus targetStatus,
        DateTimeOffset changedAt,
        DateTimeOffset? nextMaintenanceAt)
    {
        switch (targetStatus)
        {
            case DroneStatus.Available:
                drone.CompleteMaintenance(
                    changedAt,
                    nextMaintenanceAt);
                break;

            case DroneStatus.Maintenance:
                drone.SendToMaintenance(changedAt);
                break;

            case DroneStatus.Retired:
                drone.Retire(changedAt);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported target status '{targetStatus}'.");
        }
    }

    private static ChangeDroneStatusResponse MapResponse(
        Drone drone)
    {
        return new ChangeDroneStatusResponse(
            drone.Id,
            drone.Status,
            drone.LastMaintenanceAt,
            drone.NextMaintenanceAt,
            drone.UpdatedAt);
    }
}