using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantMember;

public sealed record InviteTenantMemberCommand(
    string Email) : IRequest<Result<InviteTenantMemberResponse>>;
