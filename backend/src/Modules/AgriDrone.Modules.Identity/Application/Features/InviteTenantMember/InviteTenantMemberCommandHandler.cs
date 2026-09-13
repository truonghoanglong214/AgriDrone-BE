using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantMember;

internal sealed class InviteTenantMemberCommandHandler(
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    ITenantInvitationService invitationService)
    : IRequestHandler<InviteTenantMemberCommand, Result<InviteTenantMemberResponse>>
{
    public async Task<Result<InviteTenantMemberResponse>> Handle(
        InviteTenantMemberCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId ||
            executionContext.ActorId is not Guid inviterUserId)
        {
            return Result.Failure<InviteTenantMemberResponse>(
                TenantError.ContextRequired());
        }

        var access = await effectiveAccessService.CheckTenantAsync(
            inviterUserId,
            tenantId,
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<InviteTenantMemberResponse>(
                TenantError.AccessDenied());
        }

        var result = await invitationService.InviteAsync(
            new CreateTenantInvitationRequest(
                tenantId,
                inviterUserId,
                request.Email,
                TenantMemberRole.Member,
                TenantInvitationPurpose.Membership),
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<InviteTenantMemberResponse>(result.Error);
        }

        return Result.Success(
            new InviteTenantMemberResponse(
                result.Value.InvitationId,
                result.Value.Email,
                result.Value.ExpiresAt));
    }
}
