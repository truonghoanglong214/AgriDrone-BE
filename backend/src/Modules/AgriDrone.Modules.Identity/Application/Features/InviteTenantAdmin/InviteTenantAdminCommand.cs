using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;

public sealed record InviteTenantAdminCommand(
    string Email) : IRequest<Result<InviteTenantAdminResponse>>;
