using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;

public sealed record GetFarmMembersQuery(
    Guid FarmId,
    FarmMemberRole? Role,
    GeneralStatus? Status,
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<FarmMemberListItemResponse>>>;
