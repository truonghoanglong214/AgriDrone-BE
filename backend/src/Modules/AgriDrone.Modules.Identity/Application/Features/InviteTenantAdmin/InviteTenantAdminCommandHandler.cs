using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;

internal sealed class InviteTenantAdminCommandHandler(
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    ITenantInvitationService invitationService)
    : IRequestHandler<InviteTenantAdminCommand, Result<InviteTenantAdminResponse>>
{
    public async Task<Result<InviteTenantAdminResponse>> Handle(
        InviteTenantAdminCommand request,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not Guid tenantId ||
            currentUser.UserId is not Guid inviterUserId)
        {
            return Result.Failure<InviteTenantAdminResponse>(
                TenantError.ContextRequired());
        }

        var result = await invitationService.InviteAsync(
            new CreateTenantInvitationRequest(
                tenantId,
                inviterUserId,
                request.Email,
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership),
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<InviteTenantAdminResponse>(
                result.Error);
        }

        return Result.Success(
            new InviteTenantAdminResponse(
                result.Value.InvitationId,
                result.Value.Email,
                result.Value.ExpiresAt));
    }
}
