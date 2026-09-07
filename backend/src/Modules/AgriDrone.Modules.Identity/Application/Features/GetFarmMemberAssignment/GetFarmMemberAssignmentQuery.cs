using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;

public sealed record GetFarmMemberAssignmentQuery(
    Guid FarmId,
    Guid UserId)
    : IRequest<Result<GetFarmMemberAssignmentResponse>>;
