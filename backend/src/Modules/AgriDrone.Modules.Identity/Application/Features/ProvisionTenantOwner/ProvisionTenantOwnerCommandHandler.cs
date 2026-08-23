using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

internal sealed class ProvisionTenantOwnerCommandHandler(
    ICurrentUser currentUser,
    ITenantInvitationService invitationService)
    : IRequestHandler<
        ProvisionTenantOwnerCommand,
        Result<ProvisionTenantOwnerResponse>>
{
    public async Task<Result<ProvisionTenantOwnerResponse>> Handle(
        ProvisionTenantOwnerCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid requestedByUserId)
        {
            return Result.Failure<ProvisionTenantOwnerResponse>(
                UserError.CurrentUserIsRequired());
        }

        var result = await invitationService.InviteAsync(
            new CreateTenantInvitationRequest(
                request.TenantId,
                requestedByUserId,
                request.Email,
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<ProvisionTenantOwnerResponse>(result.Error);
        }

        return Result.Success(
            new ProvisionTenantOwnerResponse(
                result.Value.InvitationId,
                result.Value.Email,
                result.Value.ExpiresAt));
    }
}
