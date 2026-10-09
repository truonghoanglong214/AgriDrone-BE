using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

internal sealed class ChangeDroneStatusCommandHandler(
    IDroneRepository droneRepository,
    IDroneMaintenanceRepository maintenanceRepository,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IMissionsUnitOfWork unitOfWork)
    : IRequestHandler<
        ChangeDroneStatusCommand,
        Result<ChangeDroneStatusResponse>>
{
    public async Task<Result<ChangeDroneStatusResponse>> Handle(
        ChangeDroneStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid userId)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.CurrentUserRequired());
        }

        var drone = await droneRepository.GetByIdAsync(
            request.DroneId,
            cancellationToken);

        if (drone is null)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.NotFound(request.DroneId));
        }

        if (request.ExpectedVersion.HasValue &&
            request.ExpectedVersion.Value != drone.Version)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                AppError.Conflict(
                    "Drone.VersionConflict",
                    "The Drone was updated by another operation. Reload and retry."));
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
        var makesDroneUnavailable =
        request.TargetStatus is
        DroneStatus.Maintenance or
        DroneStatus.Inactive or
        DroneStatus.Retired;

        if (makesDroneUnavailable &&
            await droneRepository.HasBlockingMissionAsync(
                drone.Id,
                cancellationToken))
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.HasBlockingMission(drone.Id));
        }

        var previousStatus = drone.Status;
        var previousLastMaintenanceAt = drone.LastMaintenanceAt;
        var previousNextMaintenanceAt = drone.NextMaintenanceAt;
        var changedAt = timeProvider.GetUtcNow();

        if (request.TargetStatus == DroneStatus.Available &&
            request.NextMaintenanceAt.HasValue &&
            drone.Status != DroneStatus.Maintenance)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.NextMaintenanceOnlyAfterMaintenance());
        }

        if (request.TargetStatus == DroneStatus.Available &&
            request.NextMaintenanceAt.HasValue &&
            request.NextMaintenanceAt.Value <= changedAt)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                DroneError.InvalidNextMaintenanceTime());
        }

        if (previousStatus == DroneStatus.Maintenance)
        {
            var openRecord = await maintenanceRepository.GetOpenAsync(
                drone.Id, cancellationToken);
            if (openRecord is null)
            {
                openRecord = DroneMaintenanceRecord.Start(
                    drone.Id, drone.UpdatedAt, null, "LEGACY_MAINTENANCE");
                maintenanceRepository.Add(openRecord);
            }
            openRecord.Close(changedAt, userId, request.TargetStatus,
                request.NextMaintenanceAt);
        }
        else if (request.TargetStatus == DroneStatus.Maintenance)
        {
            if (await maintenanceRepository.GetOpenAsync(drone.Id, cancellationToken)
                is not null)
                return Result.Failure<ChangeDroneStatusResponse>(
                    AppError.Conflict("Drone.MaintenanceConflict",
                        "An open maintenance record already exists."));
            maintenanceRepository.Add(DroneMaintenanceRecord.Start(
                drone.Id, changedAt, userId, request.Reason));
        }

        ApplyTransition(
            drone,
            request.TargetStatus,
            changedAt,
            request.NextMaintenanceAt);

        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString(),
                LastMaintenanceAt = previousLastMaintenanceAt,
                NextMaintenanceAt = previousNextMaintenanceAt
            });

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = drone.Status.ToString(),
                drone.LastMaintenanceAt,
                drone.NextMaintenanceAt,
                request.Reason
            });

        auditWriter.AddSystemAdminAction(
            sink: unitOfWork,
            actorId: userId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(Drone),
            entityId: drone.Id,
            action: "CHANGE_STATUS",
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DroneConcurrencyException)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                AppError.Conflict(
                    "Drone.VersionConflict",
                    "The Drone was updated by another operation. Reload and retry."));
        }
        catch (DroneMaintenanceConflictException)
        {
            return Result.Failure<ChangeDroneStatusResponse>(
                AppError.Conflict("Drone.MaintenanceConflict",
                    "Maintenance changed concurrently. Reload and retry."));
        }

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
            (DroneStatus.Available, DroneStatus.Inactive) => true,
            (DroneStatus.Inactive, DroneStatus.Available) => true,
            (DroneStatus.Available, DroneStatus.Retired) => true,
            (DroneStatus.Maintenance, DroneStatus.Retired) => true,
            (DroneStatus.Inactive, DroneStatus.Retired) => true,
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
                if (drone.Status == DroneStatus.Maintenance)
                {
                    drone.CompleteMaintenance(
                        changedAt,
                        nextMaintenanceAt);
                }
                else
                {
                    drone.Reactivate(changedAt);
                }
                break;

            case DroneStatus.Maintenance:
                drone.SendToMaintenance(changedAt);
                break;

            case DroneStatus.Inactive:
                drone.Deactivate(changedAt);
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
            drone.UpdatedAt,
            drone.Version);
    }
}
