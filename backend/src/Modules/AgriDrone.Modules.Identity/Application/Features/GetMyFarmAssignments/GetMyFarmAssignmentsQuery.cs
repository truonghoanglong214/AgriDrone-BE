using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;

public sealed record GetMyFarmAssignmentsQuery(
    FarmMemberRole? Role,
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<MyFarmAssignmentListItemResponse>>>;
