using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class GetMyAssignedFarmsQueryHandler(
    IExecutionContext executionContext,
    ISystemManagerProfileRepository profileRepository,
    IFarmManagerAssignmentRepository assignmentRepository,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    IUserRepository userRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetMyAssignedFarmsQuery, Result<IReadOnlyCollection<AssignedFarmResponse>>>
{
    public async Task<Result<IReadOnlyCollection<AssignedFarmResponse>>> Handle(
        GetMyAssignedFarmsQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<IReadOnlyCollection<AssignedFarmResponse>>(
                AuthenticationError.CurrentUserRequired());
        }

        var profile = await profileRepository.GetByUserIdAsync(actorId, cancellationToken);
        var user = await userRepository.GetByIdAsync(actorId, cancellationToken);
        if (profile is null ||
            user?.Status != UserStatus.Active ||
            !profile.CanOperate(timeProvider.GetUtcNow()))
        {
            return Result.Failure<IReadOnlyCollection<AssignedFarmResponse>>(
                SystemManagerError.NotAssignable());
        }

        var assignments = await assignmentRepository.GetActiveByProfileIdAsync(
            profile.Id,
            cancellationToken);
        var farms = await farmReferenceQuery.GetActiveFarmsAsync(
            assignments.Select(assignment => assignment.FarmId).ToArray(),
            cancellationToken);
        var assignedAtByFarm = assignments.ToDictionary(
            assignment => assignment.FarmId,
            assignment => assignment.AssignedAt);

        IReadOnlyCollection<AssignedFarmResponse> response = farms
            .Where(farm => assignedAtByFarm.ContainsKey(farm.FarmId))
            .Select(farm => new AssignedFarmResponse(
                farm.TenantId,
                farm.FarmId,
                farm.Code,
                farm.Name,
                farm.Address,
                farm.AreaHectares,
                assignedAtByFarm[farm.FarmId]))
            .ToArray();

        return Result.Success(response);
    }
}
