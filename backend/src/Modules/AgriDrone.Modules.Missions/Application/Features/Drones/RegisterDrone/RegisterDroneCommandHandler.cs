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
    .Features.Drones.RegisterDrone;

internal sealed class RegisterDroneCommandHandler(
    IDroneRepository droneRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ICurrentUser currentUser)
    : IRequestHandler<
        RegisterDroneCommand,
        Result<RegisterDroneResponse>>
{
    public async Task<Result<RegisterDroneResponse>> Handle(
        RegisterDroneCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.CurrentUserRequired());
        }

        var normalizedCode =
            request.Code.Trim().ToUpperInvariant();

        if (await droneRepository.CodeExistsAsync(
                request.TenantId,
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
                request.TenantId,
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
                request.TenantId,
                normalizedRegistration,
                cancellationToken))
        {
            return Result.Failure<RegisterDroneResponse>(
                DroneError.RegistrationNumberAlreadyExists(
                    normalizedRegistration));
        }

        var now = timeProvider.GetUtcNow();

        var drone = Drone.Create(
            request.TenantId,
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

                auditWriter.AddUserAction(
                    sink: unitOfWork,
                    tenantId: drone.TenantId,
                    farmId: null,
                    actorId: userId,
                    correlationId: executionContext.CorrelationId,
                    entityType: nameof(Drone),
                    entityId: drone.Id,
                    action: "REGISTER",
                    oldData: null,
                    newData: newData,
                    createdAt: now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MapResponse(drone));
    }

    private static RegisterDroneResponse MapResponse(
        Drone drone)
    {
        return new RegisterDroneResponse(
            drone.Id,
            drone.TenantId,
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