using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.UpdateDrone;

internal sealed class UpdateDroneCommandHandler(
    IDroneRepository droneRepository,
    IDroneRegistryUniquenessQuery uniquenessQuery,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateDroneCommand, Result<UpdateDroneResult>>
{
    public async Task<Result<UpdateDroneResult>> Handle(
        UpdateDroneCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<UpdateDroneResult>(DroneError.CurrentUserRequired());
        }

        var drone = await droneRepository.GetByIdAsync(request.DroneId, cancellationToken);
        if (drone is null)
        {
            return Result.Failure<UpdateDroneResult>(DroneError.NotFound(request.DroneId));
        }

        if (drone.Version != request.ExpectedVersion)
        {
            return Result.Failure<UpdateDroneResult>(
                AppError.Conflict(
                    "Drone.VersionConflict",
                    "The Drone was updated by another operation. Reload and retry."));
        }

        var serial = NormalizeIdentifier(request.SerialNumber);
        if (serial is not null && await uniquenessQuery.SerialNumberUsedByAnotherDroneAsync(
                serial, drone.Id, cancellationToken))
        {
            return Result.Failure<UpdateDroneResult>(DroneError.SerialNumberAlreadyExists(serial));
        }

        var registration = NormalizeIdentifier(request.RegistrationNumber);
        if (registration is not null && await uniquenessQuery.RegistrationNumberUsedByAnotherDroneAsync(
                registration, drone.Id, cancellationToken))
        {
            return Result.Failure<UpdateDroneResult>(DroneError.RegistrationNumberAlreadyExists(registration));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            drone.Name,
            drone.Model,
            drone.Manufacturer,
            drone.SerialNumber,
            drone.RegistrationNumber,
            drone.RegistrationDate,
            drone.RegistrationExpiryDate,
            drone.WeightKg,
            drone.Notes
        });

        var now = timeProvider.GetUtcNow();
        try
        {
            drone.UpdateDetails(
                request.Name,
                request.Model,
                request.Manufacturer,
                request.Specifications,
                request.SerialNumber,
                request.RegistrationNumber,
                request.RegistrationDate,
                request.RegistrationExpiryDate,
                request.WeightKg,
                request.Notes,
                now);
        }
        catch (ArgumentException)
        {
            return Result.Failure<UpdateDroneResult>(
                AppError.Validation("Drone.InvalidDetails", "Drone details are invalid."));
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<UpdateDroneResult>(
                AppError.Conflict("Drone.RetiredCannotBeEdited", "A retired Drone cannot be edited."));
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            drone.Name,
            drone.Model,
            drone.Manufacturer,
            drone.SerialNumber,
            drone.RegistrationNumber,
            drone.RegistrationDate,
            drone.RegistrationExpiryDate,
            drone.WeightKg,
            drone.Notes
        });
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(Drone),
            drone.Id,
            "UPDATE_DETAILS",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation("uq_drones_serial_number"))
        {
            return Result.Failure<UpdateDroneResult>(DroneError.SerialNumberAlreadyExists(serial!));
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation("uq_drones_registration_number"))
        {
            return Result.Failure<UpdateDroneResult>(DroneError.RegistrationNumberAlreadyExists(registration!));
        }
        catch (DroneConcurrencyException)
        {
            return Result.Failure<UpdateDroneResult>(
                AppError.Conflict(
                    "Drone.VersionConflict",
                    "The Drone was updated by another operation. Reload and retry."));
        }

        return Result.Success(new UpdateDroneResult(
            drone.Id,
            drone.Code,
            drone.Name,
            drone.Model,
            drone.Manufacturer,
            drone.Specifications,
            drone.SerialNumber,
            drone.RegistrationNumber,
            drone.RegistrationDate,
            drone.RegistrationExpiryDate,
            drone.WeightKg,
            drone.Status,
            drone.Notes,
            drone.UpdatedAt,
            drone.Version));
    }

    private static string? NormalizeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
