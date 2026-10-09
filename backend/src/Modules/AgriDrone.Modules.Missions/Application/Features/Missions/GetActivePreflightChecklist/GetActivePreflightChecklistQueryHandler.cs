using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;

internal sealed class GetActivePreflightChecklistQueryHandler(
    ISystemManagerAccessService managerAccessService,
    IDroneMissionRepository missionRepository,
    IPreflightChecklistRepository checklistRepository)
    : IRequestHandler<GetActivePreflightChecklistQuery, Result<ActivePreflightChecklistResult>>
{
    public async Task<Result<ActivePreflightChecklistResult>> Handle(
        GetActivePreflightChecklistQuery request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
        {
            return Result.Failure<ActivePreflightChecklistResult>(
                AppError.Forbidden("MissionPreflight.FarmAccessDenied", "The current SystemManager cannot access this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            tenantId,
            request.FarmId,
            cancellationToken);
        if (mission is null)
        {
            return Result.Failure<ActivePreflightChecklistResult>(
                AppError.NotFound("MissionPreflight.MissionNotFound", "Mission was not found."));
        }

        if (mission.Status != MissionStatus.Scheduled)
        {
            return Result.Failure<ActivePreflightChecklistResult>(
                AppError.Conflict("MissionPreflight.InvalidMissionState", "A checklist can only be loaded for a scheduled Mission."));
        }

        var definition = await checklistRepository.GetActiveDefinitionAsync(
            "DRONE_PRE_FLIGHT",
            cancellationToken);
        if (definition is null)
        {
            return Result.Failure<ActivePreflightChecklistResult>(
                AppError.Failure("MissionPreflight.DefinitionNotConfigured", "No active preflight checklist definition is configured."));
        }

        return Result.Success(new ActivePreflightChecklistResult(
            definition.Id,
            definition.Code,
            definition.VersionNumber,
            definition.Items.RootElement.Clone()));
    }
}
