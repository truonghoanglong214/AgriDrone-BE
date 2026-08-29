using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

namespace AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery
{
    internal sealed class TenantInvitationEmailDelivery(
    ITenantInvitationRepository invitationRepository,
    ITenantRepository tenantRepository,
    IInvitationTokenService invitationTokenService,
    IEmailSender emailSender,
    IOptions<TenantInvitationOptions> invitationOptions,
    TimeProvider timeProvider)
    : ITenantInvitationEmailDelivery
    {
        private readonly TenantInvitationOptions _options =
            invitationOptions.Value;

        public async Task<Result<TenantInvitationEmailDeliveryResult>>
            DeliverAsync(
                Guid messageTenantId,
                Guid invitationId,
                string plainTextToken,
                CancellationToken cancellationToken)
        {
            var invitation = await invitationRepository.GetByIdAsync(
                invitationId,
                cancellationToken);

            if (invitation is null)
            {
                return Result.Failure<TenantInvitationEmailDeliveryResult>(
                    AppError.NotFound(
                        "TenantInvitation.EmailDelivery.NotFound",
                        $"Invitation '{invitationId}' was not found."));
            }

            if (invitation.TenantId != messageTenantId)
            {
                return Result.Failure<TenantInvitationEmailDeliveryResult>(
                    AppError.Validation(
                        "TenantInvitation.EmailDelivery.ContextMismatch",
                        "Invitation does not belong to the message tenant."));
            }

            var expectedTokenHash =
                invitationTokenService.Hash(plainTextToken);

            if (!string.Equals(
                    expectedTokenHash,
                    invitation.TokenHash,
                    StringComparison.Ordinal))
            {
                return Result.Failure<TenantInvitationEmailDeliveryResult>(
                    AppError.Validation(
                        "TenantInvitation.EmailDelivery.TokenMismatch",
                        "Invitation token does not match the stored token hash."));
            }

            if (invitation.Status != TenantInvitationStatus.Pending)
            {
                return Result.Success(
                    TenantInvitationEmailDeliveryResult.Skipped(
                        invitation.Id,
                        invitation.Email,
                        $"Invitation status is {invitation.Status}."));
            }

            var now = timeProvider.GetUtcNow();

            if (now >= invitation.ExpiresAt)
            {
                invitation.MarkExpired(now);

                return Result.Success(
                    TenantInvitationEmailDeliveryResult.Skipped(
                        invitation.Id,
                        invitation.Email,
                        "Invitation has expired."));
            }

            var tenant =
                await tenantRepository.GetByIdIgnoreStatusAsync(
                    invitation.TenantId,
                    cancellationToken);

            if (tenant is null)
            {
                return Result.Failure<TenantInvitationEmailDeliveryResult>(
                    AppError.NotFound(
                        "TenantInvitation.EmailDelivery.TenantNotFound",
                        "The invitation tenant was not found."));
            }

            var roleDisplayName = GetRoleDisplayName(invitation.Role);
            var acceptUrl = BuildAcceptUrl(plainTextToken);

            await SendInvitationEmailAsync(
                invitation.Email,
                tenant.Name,
                roleDisplayName,
                acceptUrl,
                invitation.ExpiresAt,
                cancellationToken);

            return Result.Success(
                TenantInvitationEmailDeliveryResult.EmailSent(
                    invitation.Id,
                    invitation.Email));
        }

        private string BuildAcceptUrl(string plainTextToken)
        {
            var separator = _options.AcceptUrl.Contains('?')
                ? '&'
                : '?';

            return $"{_options.AcceptUrl}{separator}token=" +
                   Uri.EscapeDataString(plainTextToken);
        }

        private async Task SendInvitationEmailAsync(
            string email,
            string tenantName,
            string roleDisplayName,
            string acceptUrl,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            var encodedTenantName =
                HtmlEncoder.Default.Encode(tenantName);
            var encodedRole =
                HtmlEncoder.Default.Encode(roleDisplayName);
            var encodedAcceptUrl =
                HtmlEncoder.Default.Encode(acceptUrl);

            var message = new EmailMessage(
                To: [new EmailRecipient(email)],
                Subject:
                    $"Invitation to join {tenantName} as {roleDisplayName}",
                HtmlBody: $"""
                <h2>AgriDrone tenant invitation</h2>
                <p>
                    You have been invited to join
                    <strong>{encodedTenantName}</strong>
                    as <strong>{encodedRole}</strong>.
                </p>
                <p>
                    <a href="{encodedAcceptUrl}">
                        Accept invitation
                    </a>
                </p>
                <p>This invitation expires at {expiresAt:O}.</p>
                """,
                TextBody:
                    $"You have been invited to join {tenantName} " +
                    $"as {roleDisplayName}.{Environment.NewLine}" +
                    $"Accept the invitation: {acceptUrl}" +
                    $"{Environment.NewLine}" +
                    $"Expires at: {expiresAt:O}");

            await emailSender.SendAsync(message, cancellationToken);
        }

        private static string GetRoleDisplayName(
            TenantMemberRole role) =>
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
}
