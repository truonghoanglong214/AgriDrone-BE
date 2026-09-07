using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries;

internal interface IFarmMembershipQueries
{
    Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        CancellationToken cancellationToken);
}
