using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class SystemManagerAccessService(
    IExecutionContext executionContext,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    ISystemManagerProfileRepository profileRepository,
    IFarmManagerAssignmentRepository assignmentRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider) : ISystemManagerAccessService
{
    public async Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return SystemManagerFarmAccess.Denied("An authenticated system actor is required.");
        }

        var farm = await farmReferenceQuery.GetActiveFarmAsync(
            farmId,
            cancellationToken);
        if (farm is null)
        {
            return SystemManagerFarmAccess.Denied("The Farm is not active or does not exist.");
        }

        var profile = await profileRepository.GetByUserIdAsync(
            actorId,
            cancellationToken);
        var user = await userRepository.GetByIdAsync(actorId, cancellationToken);
        if (profile is null ||
            user?.Status != UserStatus.Active ||
            !profile.CanOperate(timeProvider.GetUtcNow()))
        {
            return SystemManagerFarmAccess.Denied(
                "The SystemManager profile is not active and flight-qualified.");
        }

        var assignment = await assignmentRepository.GetActiveByFarmIdAsync(
            farmId,
            cancellationToken);
        if (assignment is null ||
            assignment.SystemManagerProfileId != profile.Id ||
            assignment.TenantId != farm.TenantId)
        {
            return SystemManagerFarmAccess.Denied(
                "The SystemManager is not the active primary manager for this Farm.");
        }

        return SystemManagerFarmAccess.Allowed(
            farm.TenantId,
            farm.FarmId,
            profile.Id);
    }
}
