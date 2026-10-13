using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionUploadReadiness;

internal sealed class GetMissionUploadReadinessQueryHandler(
    IDroneMissionRepository missions,
    IMissionUploadReadinessQueries readinessQueries,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<
        GetMissionUploadReadinessQuery,
        Result<MissionUploadReadinessResult>>
{
    public async Task<Result<MissionUploadReadinessResult>> Handle(
        GetMissionUploadReadinessQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<MissionUploadReadinessResult>(
                MissionError.CurrentTenantRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != tenantId || access.FarmId != request.FarmId)
            return Result.Failure<MissionUploadReadinessResult>(AppError.Forbidden(
                "MissionUpload.FarmAccessDenied", "The manager is not assigned to this Farm."));

        var mission = await missions.GetByIdAsync(
            request.MissionId,
            tenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<MissionUploadReadinessResult>(
                MissionError.NotFound(request.MissionId));
        }

        var snapshot = await readinessQueries.GetAsync(
            tenantId,
            request.FarmId,
            request.MissionId,
            timeProvider.GetUtcNow(),
            cancellationToken);

        var blockers = MissionUploadReadinessPolicy.Evaluate(
            mission,
            snapshot);

        return Result.Success(new MissionUploadReadinessResult(
            mission.Id,
            mission.Version,
            mission.Status,
            snapshot.RawImageCount,
            snapshot.RawVideoCount,
            snapshot.PersistedPointCount,
            snapshot.HasActiveUploadSessions,
            blockers));
    }
}
