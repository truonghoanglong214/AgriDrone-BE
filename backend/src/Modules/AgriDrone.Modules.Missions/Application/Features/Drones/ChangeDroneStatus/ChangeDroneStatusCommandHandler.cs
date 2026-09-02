using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

internal sealed class ChangeDroneStatusCommandHandler(
    IDroneRepository droneRepository,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
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
        var changedAt = timeProvider.GetUtcNow();

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

        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString()
            });

                using var newData =
                    JsonSerializer.SerializeToDocument(new
                    {
                        Status = drone.Status.ToString(),
                        drone.LastMaintenanceAt,
                        drone.NextMaintenanceAt
                    });

                auditWriter.AddUserAction(
                    sink: unitOfWork,
                    tenantId: drone.TenantId,
                    farmId: null,
                    actorId: userId,
                    correlationId: executionContext.CorrelationId,
                    entityType: nameof(Drone),
                    entityId: drone.Id,
                    action: "CHANGE_STATUS",
                    oldData: oldData,
                    newData: newData,
                    createdAt: changedAt);

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