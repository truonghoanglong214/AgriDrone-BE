using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.CreateMission;

internal sealed class CreateMissionCommandHandler(
    IDroneMissionRepository missionRepository,
    IDroneRepository droneRepository,
    IMissionPlanningReferenceQuery referenceQuery,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        CreateMissionCommand,
        Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        CreateMissionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<MissionResponse>(
                MissionError.CurrentUserRequired());
        }

        if (!await referenceQuery.IsActiveZoneAsync(
                request.TenantId,
                request.FarmId,
                request.ZoneId,
                cancellationToken))
        {
            return Result.Failure<MissionResponse>(
                MissionError.ZoneNotActive(request.ZoneId));
        }

        if (request.MissionType == MissionType.HealthInspection &&
            request.SourceMapVersionId is Guid sourceMapVersionId &&
            !await referenceQuery.IsConfirmedMapVersionAsync(
                request.TenantId,
                request.FarmId,
                request.ZoneId,
                sourceMapVersionId,
                cancellationToken))
        {
            return Result.Failure<MissionResponse>(
                MissionError.SourceMapNotConfirmed(sourceMapVersionId));
        }

        var drone = await droneRepository.GetByIdAsync(
            request.DroneId,
            request.TenantId,
            cancellationToken);

        if (drone is null)
        {
            return Result.Failure<MissionResponse>(
                MissionError.DroneNotFound(request.DroneId));
        }

        var normalizedCode =
            request.MissionCode.Trim().ToUpperInvariant();

        if (await missionRepository.CodeExistsAsync(
                request.FarmId,
                normalizedCode,
                cancellationToken))
        {
            return Result.Failure<MissionResponse>(
                MissionError.CodeAlreadyExists(normalizedCode));
        }

        using var emptyParameters =
            JsonDocument.Parse("{}");

        using var suppliedParameters =
            request.FlightParameters.HasValue
                ? JsonDocument.Parse(
                    request.FlightParameters.Value.GetRawText())
                : null;

        var now = timeProvider.GetUtcNow();

        var mission = DroneMission.Create(
            request.TenantId,
            request.FarmId,
            request.ZoneId,
            request.DroneId,
            request.PilotUserId,
            request.MissionCode,
            request.MissionType,
            request.SourceMapVersionId,
            suppliedParameters ?? emptyParameters,
            request.Notes,
            actorId,
            now);

        missionRepository.Add(mission);

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                mission.FarmId,
                mission.ZoneId,
                mission.DroneId,
                mission.MissionCode,
                MissionType = mission.MissionType.ToString(),
                Status = mission.Status.ToString(),
                mission.SourceMapVersionId
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "CREATE",
            oldData: null,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionCodeConflictException)
        {
            return Result.Failure<MissionResponse>(
                MissionError.CodeAlreadyExists(
                    mission.MissionCode));
        }

        return Result.Success(
            MissionResponseMapper.Map(mission));
    }
}
