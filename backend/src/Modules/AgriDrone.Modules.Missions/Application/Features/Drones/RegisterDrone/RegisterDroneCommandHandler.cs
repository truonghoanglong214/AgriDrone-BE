using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;

internal sealed class RegisterDroneCommandHandler(
    IDroneRepository droneRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        RegisterDroneCommand,
        Result<RegisterDroneResponse>>
{
    private const string DroneCodeConstraint =
        "uq_drones_code";
    private const string DroneSerialNumberConstraint =
        "uq_drones_serial_number";
    private const string DroneRegistrationNumberConstraint =
        "uq_drones_registration_number";

    public async Task<Result<RegisterDroneResponse>> Handle(
        RegisterDroneCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid userId)
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.CurrentUserRequired());
        }

        var normalizedCode =
            request.Code.Trim().ToUpperInvariant();

        if (await droneRepository.CodeExistsAsync(
                normalizedCode,
                cancellationToken))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.CodeAlreadyExists(normalizedCode));
        }

        var normalizedSerial =
            NormalizeIdentifier(request.SerialNumber);

        if (normalizedSerial is not null &&
            await droneRepository.SerialNumberExistsAsync(
                normalizedSerial,
                cancellationToken))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.SerialNumberAlreadyExists(
                    normalizedSerial));
        }

        var normalizedRegistration =
            NormalizeIdentifier(request.RegistrationNumber);

        if (normalizedRegistration is not null &&
            await droneRepository.RegistrationNumberExistsAsync(
                normalizedRegistration,
                cancellationToken))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.RegistrationNumberAlreadyExists(
                    normalizedRegistration));
        }

        var now = timeProvider.GetUtcNow();

        var drone = Drone.Create(
            request.Code,
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
        droneRepository.Add(drone);
        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = drone.Status.ToString(),
                drone.Code,
                drone.Name
            });

        auditWriter.AddSystemAdminAction(
            sink: unitOfWork,
            actorId: userId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(Drone),
            entityId: drone.Id,
            action: "REGISTER",
            oldData: null,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                DroneCodeConstraint))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.CodeAlreadyExists(normalizedCode));
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                DroneSerialNumberConstraint))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.SerialNumberAlreadyExists(
                    normalizedSerial!));
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                DroneRegistrationNumberConstraint))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.RegistrationNumberAlreadyExists(
                    normalizedRegistration!));
        }

        return Result.Success(MapResponse(drone));
    }

    private static RegisterDroneResponse MapResponse(
        Drone drone)
    {
        return new RegisterDroneResponse(
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
            drone.CreatedAt);
    }

    private static string? NormalizeIdentifier(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }
}
