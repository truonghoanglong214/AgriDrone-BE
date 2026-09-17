using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;
using AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries;

internal interface IFarmMembershipQueries
{
    Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<PagedResult<FarmMemberListItemResponse>> GetMembersPageAsync(
        Guid tenantId,
        Guid farmId,
        FarmMemberRole? role,
        GeneralStatus? status,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<PagedResult<MyFarmAssignmentReadModel>> GetMyAssignmentsPageAsync(
        Guid tenantId,
        Guid userId,
        IReadOnlyCollection<Guid> activeFarmIds,
        FarmMemberRole? role,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);
}
