using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.ScheduleMission;

internal sealed class ScheduleMissionCommandHandler(
    IDroneMissionRepository missionRepository,
    IDroneQueries droneQueries,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        ScheduleMissionCommand,
        Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        ScheduleMissionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<MissionResponse>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<MissionResponse>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.Version != request.ExpectedVersion)
        {
            return Result.Failure<MissionResponse>(
                MissionError.VersionConflict(
                    request.ExpectedVersion,
                    mission.Version));
        }

        if (mission.Status != MissionStatus.Draft)
        {
            return Result.Failure<MissionResponse>(
                MissionError.InvalidTransition(
                    mission.Status,
                    MissionStatus.Scheduled));
        }

        var availableDrones =
            await droneQueries.GetAvailableAsync(
                mission.TenantId,
                request.ScheduledAt,
                request.ScheduledEndAt,
                cancellationToken);

        var droneIsAvailable = availableDrones.Any(
            drone => drone.Id == mission.DroneId);

        if (!droneIsAvailable)
        {
            return Result.Failure<MissionResponse>(
                MissionError.DroneNotAvailable(
                    mission.DroneId));
        }

        var previousStatus = mission.Status;
        var previousScheduledAt = mission.ScheduledAt;
        var previousScheduledEndAt =
            mission.ScheduledEndAt;

        var now = timeProvider.GetUtcNow();

        mission.Schedule(
            request.ScheduledAt,
            request.ScheduledEndAt,
            now);

        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString(),
                ScheduledAt = previousScheduledAt,
                ScheduledEndAt =
                    previousScheduledEndAt
            });

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = mission.Status.ToString(),
                mission.ScheduledAt,
                mission.ScheduledEndAt,
                mission.DroneId
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "SCHEDULE",
            oldData: oldData,
            newData: newData,
            createdAt: now);

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<MissionResponse>(
                MissionError.ConcurrentUpdate());
        }
        catch (MissionScheduleConflictException)
        {
            return Result.Failure<MissionResponse>(
                MissionError.ScheduleConflict(
                    mission.DroneId));
        }

        return Result.Success(
            MissionResponseMapper.Map(mission));
    }
}
