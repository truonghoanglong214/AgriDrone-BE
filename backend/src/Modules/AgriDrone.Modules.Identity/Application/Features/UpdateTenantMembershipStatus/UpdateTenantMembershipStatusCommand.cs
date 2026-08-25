using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantMembershipStatus;

public sealed record UpdateTenantMembershipStatusCommand(
    Guid UserId,
    GeneralStatus Status)
    : IRequest<Result>;
