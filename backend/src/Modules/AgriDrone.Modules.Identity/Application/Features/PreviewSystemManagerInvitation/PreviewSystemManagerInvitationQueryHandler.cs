using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewSystemManagerInvitation;

internal sealed class PreviewSystemManagerInvitationQueryHandler(
    IInvitationTokenService invitationTokenService,
    ISystemManagerInvitationRepository invitationRepository,
    IUserRepository userRepository,
    ISystemManagerProfileRepository profileRepository,
    TimeProvider timeProvider)
    : IRequestHandler<
        PreviewSystemManagerInvitationQuery,
        Result<PreviewSystemManagerInvitationResponse>>
{
    public async Task<Result<PreviewSystemManagerInvitationResponse>>
        Handle(
            PreviewSystemManagerInvitationQuery request,
            CancellationToken cancellationToken)
    {
        var tokenHash = invitationTokenService.Hash(
            request.Token.Trim());

        var invitation =
            await invitationRepository.GetByTokenHashAsync(
                tokenHash,
                cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (invitation is null || !invitation.CanBeAccepted(now))
        {
            return Result.Failure<
                PreviewSystemManagerInvitationResponse>(
                SystemManagerInvitationError.InvalidOrExpired());
        }

        var user = await userRepository.GetByEmailAsync(
            invitation.Email,
            cancellationToken);

        if (user is not null)
        {
            if (user.Status != UserStatus.Active)
            {
                return Result.Failure<
                    PreviewSystemManagerInvitationResponse>(
                    SystemManagerInvitationError.UserInactive());
            }

            var profile = await profileRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

            if (profile is not null)
            {
                return Result.Failure<
                    PreviewSystemManagerInvitationResponse>(
                    SystemManagerInvitationError.AlreadySystemManager());
            }
        }

        return Result.Success(
            new PreviewSystemManagerInvitationResponse(
                MaskEmail(invitation.Email),
                "SYSTEM_MANAGER",
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