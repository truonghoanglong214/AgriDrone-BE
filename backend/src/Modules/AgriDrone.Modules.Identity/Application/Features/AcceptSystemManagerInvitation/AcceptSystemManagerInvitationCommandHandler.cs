using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptSystemManagerInvitation;

internal sealed class AcceptSystemManagerInvitationCommandHandler(
    IInvitationTokenService invitationTokenService,
    ISystemManagerInvitationRepository invitationRepository,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISystemManagerProfileRepository profileRepository,
    IPasswordService passwordService,
    TimeProvider timeProvider,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<
        AcceptSystemManagerInvitationCommand,
        Result<AcceptSystemManagerInvitationResponse>>
{
    public async Task<Result<AcceptSystemManagerInvitationResponse>>
        Handle(
            AcceptSystemManagerInvitationCommand request,
            CancellationToken cancellationToken)
    {
        var tokenHash = invitationTokenService.Hash(
            request.Token.Trim());

        return await unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => AcceptAsync(
                request,
                tokenHash,
                transactionCancellationToken),
            cancellationToken);
    }

    private async Task<Result<AcceptSystemManagerInvitationResponse>>
        AcceptAsync(
            AcceptSystemManagerInvitationCommand request,
            string tokenHash,
            CancellationToken cancellationToken)
    {
        var invitation =
            await invitationRepository.GetByTokenHashAsync(
                tokenHash,
                cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (invitation is null || !invitation.CanBeAccepted(now))
        {
            return Result.Failure<
                AcceptSystemManagerInvitationResponse>(
                    SystemManagerInvitationError.InvalidOrExpired());
        }

        var user = await userRepository.GetByEmailIncludingDeletedAsync(
            invitation.Email,
            cancellationToken);

        var accountCreated = false;

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 8)
            {
                return Result.Failure<
                    AcceptSystemManagerInvitationResponse>(
                        SystemManagerInvitationError
                            .RegistrationDetailsRequired());
            }

            user = User.Create(
                invitation.Email,
                passwordService.HashPassword(request.Password),
                request.FullName.Trim(),
                NormalizePhone(request.Phone),
                UserStatus.Active,
                now);

            userRepository.Add(user);
            accountCreated = true;
        }
        else if (user.DeletedAt is not null ||
                 user.Status != UserStatus.Active)
        {
            return Result.Failure<
                AcceptSystemManagerInvitationResponse>(
                    SystemManagerInvitationError.UserInactive());
        }

        var existingProfile =
            await profileRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

        if (existingProfile is not null)
        {
            return Result.Failure<
                AcceptSystemManagerInvitationResponse>(
                    SystemManagerInvitationError
                        .AlreadySystemManager());
        }

        var systemManagerRole = await roleRepository.GetByCodeAsync(
            SystemRoles.SystemManager,
            cancellationToken);

        if (systemManagerRole is null)
        {
            return Result.Failure<
                AcceptSystemManagerInvitationResponse>(
                    SystemManagerError.RoleMissing());
        }

        var existingRoleCodes =
            await userRepository.GetSystemRoleCodesAsync(
                user.Id,
                cancellationToken);

        if (!existingRoleCodes.Contains(
                SystemRoles.SystemManager,
                StringComparer.OrdinalIgnoreCase))
        {
            user.AssignSystemRole(systemManagerRole.Id, now);
        }

        var profile = SystemManagerProfile.Create(user.Id, now);

        profileRepository.Add(profile);
        invitation.Accept(user.Id, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new AcceptSystemManagerInvitationResponse(
                user.Id,
                profile.Id,
                accountCreated));
    }

    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim();
}
