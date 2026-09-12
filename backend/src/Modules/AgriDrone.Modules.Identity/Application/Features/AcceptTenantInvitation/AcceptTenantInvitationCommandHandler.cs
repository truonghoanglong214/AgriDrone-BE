using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Abstractions.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;

internal sealed class AcceptTenantInvitationCommandHandler(
    IInvitationTokenService invitationTokenService,
    ITenantInvitationRepository tenantInvitationRepository,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IPasswordService passwordService,
    IIdentityIntegrationOutbox integrationOutbox,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<
        AcceptTenantInvitationCommand,
        Result<AcceptTenantInvitationResponse>>
{
    public async Task<Result<AcceptTenantInvitationResponse>> Handle(
        AcceptTenantInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = invitationTokenService.Hash(request.Token);

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => AcceptAsync(
                    request,
                    tokenHash,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (ActiveTenantOwnerConflictException)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.OwnerAlreadyAssigned());
        }
    }

    private async Task<Result<AcceptTenantInvitationResponse>> AcceptAsync(
        AcceptTenantInvitationCommand request,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        var invitation = await tenantInvitationRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (invitation is null || !invitation.CanBeAccepted(now))
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.InvalidOrExpired());
        }

        if (invitation.Purpose == TenantInvitationPurpose.OwnerProvisioning &&
            await tenantMembershipRepository.HasActiveOwnerAsync(
                invitation.TenantId,
                cancellationToken))
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantInvitationError.OwnerAlreadyAssigned());
        }

        var tenant = await tenantRepository.GetByIdIgnoreStatusAsync(
            invitation.TenantId,
            cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(
                TenantError.NotFound());
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

        var notificationId = Guid.NewGuid();
        var payload = new EmailNotificationRequestedV1(
            NotificationId: notificationId,
            TemplateKey: EmailTemplateKeys.TenantWelcome,
            Recipients:
            [
                new EmailRecipientV1(user.Email, user.FullName)
            ],
            Variables: new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = user.FullName,
                [EmailTemplateVariableKeys.TenantName] = tenant.Name,
                [EmailTemplateVariableKeys.RoleName] =
                    GetRoleDisplayName(invitation.Role)
            });
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.EmailNotificationRequestedV1,
            messageId: Guid.NewGuid(),
            correlationId: executionContext.CorrelationId,
            tenantId: invitation.TenantId,
            actorId: user.Id,
            occurredAt: now,
            payload);

        integrationOutbox.Add(
            envelope,
            partitionKey: notificationId.ToString("D"));

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

    private static string GetRoleDisplayName(TenantMemberRole role) =>
        role switch
        {
            TenantMemberRole.Owner => "Tenant Owner",
            TenantMemberRole.TenantAdmin => "Tenant Admin",
            TenantMemberRole.Member => "Tenant Member",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unsupported tenant role.")
        };
}
