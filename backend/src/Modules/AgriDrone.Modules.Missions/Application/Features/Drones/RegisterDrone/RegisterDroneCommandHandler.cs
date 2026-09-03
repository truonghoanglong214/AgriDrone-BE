using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;

internal sealed class RegisterDroneCommandHandler(
    IDroneRepository droneRepository,
    IDroneStatusChangeRepository statusChangeRepository,
    IMissionsUnitOfWork unitOfWork,
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

        var now = DateTimeOffset.UtcNow;

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

        var initialStatusChange = DroneStatusChange.Create(
            drone.TenantId,
            drone.Id,
            previousStatus: null,
            newStatus: drone.Status,
            changedBy: userId,
            changedAt: now);

        droneRepository.Add(drone);
        statusChangeRepository.Add(initialStatusChange);

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