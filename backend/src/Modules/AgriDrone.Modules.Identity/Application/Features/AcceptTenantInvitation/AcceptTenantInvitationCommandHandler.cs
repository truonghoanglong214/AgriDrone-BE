using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;

internal sealed class AcceptTenantInvitationCommandHandler(
    IInvitationTokenService invitationTokenService,
    ITenantInvitationRepository tenantInvitationRepository,
    IUserRepository userRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IPasswordService passwordService,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<
        AcceptTenantInvitationCommand,
        Result<AcceptTenantInvitationResponse>>
{
    public Task<Result<AcceptTenantInvitationResponse>> Handle(
        AcceptTenantInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = invitationTokenService.Hash(request.Token);

        return unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => AcceptAsync(
                request,
                tokenHash,
                transactionCancellationToken),
            cancellationToken);
    }

    private async Task<Result<AcceptTenantInvitationResponse>> AcceptAsync(
        AcceptTenantInvitationCommand request,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var invitation = await tenantInvitationRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (invitation is null || !invitation.CanBeAccepted(now))
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.InvalidOrExpired());
        }

        var user = await userRepository.GetByEmailAsync(
            invitation.Email,
            cancellationToken);
        var accountCreated = false;

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 8 ||
                string.IsNullOrWhiteSpace(request.FullName))
            {
                return Result.Failure<AcceptTenantInvitationResponse>(
                    TenantInvitationError.RegistrationDetailsRequired());
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
        else if (user.Status != UserStatus.Active)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.UserInactive());
        }

        var existingMembership =
            await tenantMembershipRepository.GetByUserAndTenantIdAsync(
                user.Id,
                invitation.TenantId,
                cancellationToken);

        if (existingMembership is not null)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.UserAlreadyMember());
        }

        var membership = TenantMembership.Create(
            invitation.TenantId,
            user.Id,
            invitation.Role,
            GeneralStatus.Active,
            now,
            now);

        tenantMembershipRepository.Add(membership);
        invitation.Accept(user.Id, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new AcceptTenantInvitationResponse(
                user.Id,
                invitation.TenantId,
                invitation.Role,
                accountCreated));
    }

    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}
