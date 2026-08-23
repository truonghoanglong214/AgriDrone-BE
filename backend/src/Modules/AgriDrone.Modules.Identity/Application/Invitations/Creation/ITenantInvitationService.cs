using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal interface ITenantInvitationService
{
    Task<Result<TenantInvitationCreated>> InviteAsync(
        CreateTenantInvitationRequest request,
        CancellationToken cancellationToken);
}
