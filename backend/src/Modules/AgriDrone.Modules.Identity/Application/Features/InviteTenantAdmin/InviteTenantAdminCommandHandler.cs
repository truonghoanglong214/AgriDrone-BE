using System.Text.Encodings.Web;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using MediatR;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;

internal sealed class InviteTenantAdminCommandHandler(
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    ITenantInvitationRepository tenantInvitationRepository,
    IInvitationTokenService invitationTokenService,
    IEmailSender emailSender,
    IOptions<TenantInvitationOptions> invitationOptions,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<InviteTenantAdminCommand, Result<InviteTenantAdminResponse>>
{
    private readonly TenantInvitationOptions _invitationOptions = invitationOptions.Value;

    public Task<Result<InviteTenantAdminResponse>> Handle(
        InviteTenantAdminCommand request,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not Guid tenantId ||
            currentUser.UserId is not Guid inviterUserId)
        {
            return Task.FromResult(
                Result.Failure<InviteTenantAdminResponse>(
                    UserError.CurrentTenantRequired()));
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (string.Equals(
                email,
                currentUser.Email?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                Result.Failure<InviteTenantAdminResponse>(
                    TenantInvitationError.InviteSelfNotAllowed()));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => InviteAsync(
                tenantId,
                inviterUserId,
                email,
                transactionCancellationToken),
            cancellationToken);
    }

    private async Task<Result<InviteTenantAdminResponse>> InviteAsync(
        Guid tenantId,
        Guid inviterUserId,
        string email,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(
            tenantId,
            cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<InviteTenantAdminResponse>(
                UserError.TenantNotFound());
        }

        var existingUser = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (existingUser is not null)
        {
            var membership =
                await tenantMembershipRepository.GetByUserAndTenantIdAsync(
                    existingUser.Id,
                    tenantId,
                    cancellationToken);

            if (membership is not null)
            {
                return Result.Failure<InviteTenantAdminResponse>(
                    TenantInvitationError.UserAlreadyMember());
            }
        }

        var now = DateTimeOffset.UtcNow;
        var pendingInvitation = await tenantInvitationRepository.GetPendingAsync(
            tenantId,
            email,
            cancellationToken);

        if (pendingInvitation is not null)
        {
            if (pendingInvitation.CanBeAccepted(now))
            {
                return Result.Failure<InviteTenantAdminResponse>(
                    TenantInvitationError.AlreadyPending());
            }

            pendingInvitation.MarkExpired(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var token = invitationTokenService.Generate();
        var expiresAt = now.AddHours(_invitationOptions.ExpirationHours);
        var invitation = TenantInvitation.Create(
            tenantId,
            email,
            TenantMemberRole.TenantAdmin,
            token.TokenHash,
            inviterUserId,
            expiresAt,
            now);

        tenantInvitationRepository.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var acceptUrl = BuildAcceptUrl(token.PlainTextToken);
        await SendInvitationEmailAsync(
            email,
            tenant.Name,
            acceptUrl,
            expiresAt,
            cancellationToken);

        return Result.Success(
            new InviteTenantAdminResponse(
                invitation.Id,
                invitation.Email,
                invitation.ExpiresAt));
    }

    private string BuildAcceptUrl(string plainTextToken)
    {
        var separator = _invitationOptions.AcceptUrl.Contains('?')
            ? '&'
            : '?';

        return $"{_invitationOptions.AcceptUrl}{separator}token={Uri.EscapeDataString(plainTextToken)}";
    }

    private async Task SendInvitationEmailAsync(
        string email,
        string tenantName,
        string acceptUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var encodedTenantName = HtmlEncoder.Default.Encode(tenantName);
        var encodedAcceptUrl = HtmlEncoder.Default.Encode(acceptUrl);

        var message = new EmailMessage(
            To: [new EmailRecipient(email)],
            Subject: $"Invitation to join {tenantName} as Tenant Admin",
            HtmlBody: $"""
                <h2>AgriDrone tenant invitation</h2>
                <p>You have been invited to join <strong>{encodedTenantName}</strong> as Tenant Admin.</p>
                <p><a href="{encodedAcceptUrl}">Accept invitation</a></p>
                <p>This invitation expires at {expiresAt:O}.</p>
                """,
            TextBody:
                $"You have been invited to join {tenantName} as Tenant Admin.{Environment.NewLine}" +
                $"Accept the invitation: {acceptUrl}{Environment.NewLine}" +
                $"Expires at: {expiresAt:O}");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
