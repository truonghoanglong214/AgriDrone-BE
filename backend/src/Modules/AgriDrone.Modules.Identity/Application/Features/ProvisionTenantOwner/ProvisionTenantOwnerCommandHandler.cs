using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Provisioning;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

internal sealed class ProvisionTenantOwnerCommandHandler(
    IExecutionContext executionContext,
    ITenantOwnerInvitationPort invitationPort)
    : IRequestHandler<
        ProvisionTenantOwnerCommand,
        Result<ProvisionTenantOwnerResponse>>
{
    public async Task<Result<ProvisionTenantOwnerResponse>> Handle(
        ProvisionTenantOwnerCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid requestedByUserId)
        {
            return Result.Failure<ProvisionTenantOwnerResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var result = await invitationPort.InviteAsync(
            new InviteTenantOwnerRequest(
                request.TenantId,
                requestedByUserId,
                request.Email),
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
