using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;

internal sealed class PreviewTenantInvitationQueryHandler(
    IInvitationTokenService invitationTokenService,
    ITenantInvitationRepository tenantInvitationRepository,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider)
    : IRequestHandler<
        PreviewTenantInvitationQuery,
        Result<PreviewTenantInvitationResponse>>
{
    public async Task<Result<PreviewTenantInvitationResponse>> Handle(
        PreviewTenantInvitationQuery request,
        CancellationToken cancellationToken)
    {
        var tokenHash = invitationTokenService.Hash(request.Token);
        var invitation = await tenantInvitationRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (invitation is null ||
            !invitation.CanBeAccepted(timeProvider.GetUtcNow()))
        {
            return Result.Failure<PreviewTenantInvitationResponse>(
                TenantInvitationError.InvalidOrExpired());
        }

        var tenant = await tenantRepository.GetByIdIgnoreStatusAsync(
            invitation.TenantId,
            cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<PreviewTenantInvitationResponse>(
                TenantError.NotFound());
        }

        if (tenant.Status != GeneralStatus.Active)
        {
            return Result.Failure<PreviewTenantInvitationResponse>(
                TenantError.Inactive());
        }

        var user = await userRepository.GetByEmailAsync(
            invitation.Email,
            cancellationToken);

        if (user is not null && user.Status != UserStatus.Active)
        {
            return Result.Failure<PreviewTenantInvitationResponse>(
                TenantInvitationError.UserInactive());
        }

        return Result.Success(
            new PreviewTenantInvitationResponse(
                MaskEmail(invitation.Email),
                tenant.Name,
                invitation.Role,
                invitation.ExpiresAt,
                RequiresAccountCreation: user is null));
    }

    private static string MaskEmail(string email)
    {
        var separatorIndex = email.IndexOf('@');

        if (separatorIndex <= 0)
        {
            return "***";
        }

        return $"{email[0]}***{email[separatorIndex..]}";
    }
}
