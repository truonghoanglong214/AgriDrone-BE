using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed class TenantInvitationService(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    ITenantInvitationRepository tenantInvitationRepository,
    IInvitationTokenService invitationTokenService,
    IIdentityIntegrationOutbox integrationOutbox,
    IExecutionContext executionContext,
    IOptions<TenantInvitationOptions> invitationOptions,
    TimeProvider timeProvider,
    IIdentityUnitOfWork unitOfWork)
    : ITenantInvitationService
{
    private readonly TenantInvitationOptions _invitationOptions =
        invitationOptions.Value;

    public async Task<Result<TenantInvitationCreated>> InviteAsync(
        CreateTenantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => InviteCoreAsync(
                    request,
                    email,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (PendingTenantInvitationConflictException)
        {
            return Result.Failure<TenantInvitationCreated>(
                TenantInvitationError.AlreadyPending());
        }
        catch (PendingTenantOwnerProvisioningConflictException)
        {
            return Result.Failure<TenantInvitationCreated>(
                TenantInvitationError.OwnerProvisioningAlreadyPending());
        }
    }

    private async Task<Result<TenantInvitationCreated>> InviteCoreAsync(
        CreateTenantInvitationRequest request,
        string email,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(
            request.TenantId,
            cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantInvitationCreated>(
                UserError.TenantNotFound());
        }

        if (request.Purpose == TenantInvitationPurpose.OwnerProvisioning &&
            await tenantMembershipRepository.HasActiveOwnerAsync(
                request.TenantId,
                cancellationToken))
        {
            return Result.Failure<TenantInvitationCreated>(
                TenantInvitationError.OwnerAlreadyAssigned());
        }

        var existingUser = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (existingUser?.Id == request.InvitedByUserId)
        {
            return Result.Failure<TenantInvitationCreated>(
                TenantInvitationError.InviteSelfNotAllowed());
        }

        if (existingUser is not null)
        {
            var membership =
                await tenantMembershipRepository.GetByUserAndTenantIdAsync(
                    existingUser.Id,
                    request.TenantId,
                    cancellationToken);

            if (membership is not null)
            {
                return Result.Failure<TenantInvitationCreated>(
                    TenantInvitationError.UserAlreadyMember());
            }
        }

        var now = timeProvider.GetUtcNow();
        var pendingInvitation = request.Purpose ==
            TenantInvitationPurpose.OwnerProvisioning
                ? await tenantInvitationRepository
                    .GetPendingOwnerProvisioningAsync(
                        request.TenantId,
                        cancellationToken)
                : await tenantInvitationRepository.GetPendingAsync(
                    request.TenantId,
                    email,
                    cancellationToken);

        if (pendingInvitation is not null)
        {
            if (pendingInvitation.CanBeAccepted(now))
            {
                return Result.Failure<TenantInvitationCreated>(
                    request.Purpose == TenantInvitationPurpose.OwnerProvisioning
                        ? TenantInvitationError.OwnerProvisioningAlreadyPending()
                        : TenantInvitationError.AlreadyPending());
            }

            pendingInvitation.MarkExpired(now);

            // Free the filtered unique index before inserting its replacement.
            // The outer transaction keeps the update and insert atomic.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var token = invitationTokenService.Generate();
        var expiresAt = now.AddHours(
            _invitationOptions.ExpirationHours);
        var invitation = TenantInvitation.Create(
            request.TenantId,
            email,
            request.Role,
            request.Purpose,
            token.TokenHash,
            request.InvitedByUserId,
            expiresAt,
            now);

        tenantInvitationRepository.Add(invitation);

        var payload = new TenantInvitationEmailRequestedV1(
            invitation.Id,
            token.PlainTextToken);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.TenantInvitationEmailRequestedV1,
            messageId: Guid.NewGuid(),
            correlationId: executionContext.CorrelationId,
            tenantId: request.TenantId,
            actorId: request.InvitedByUserId,
            occurredAt: now,
            payload);

        integrationOutbox.Add(
            envelope,
            partitionKey: invitation.Id.ToString("D"));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new TenantInvitationCreated(
                invitation.Id,
                invitation.Email,
                invitation.ExpiresAt));
    }
}
