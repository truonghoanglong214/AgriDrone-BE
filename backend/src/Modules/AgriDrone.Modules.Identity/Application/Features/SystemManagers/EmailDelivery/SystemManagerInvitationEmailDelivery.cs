using System.Text.Encodings.Web;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;

internal sealed class SystemManagerInvitationEmailDelivery(
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> passwordResetOptions)
    : ISystemManagerInvitationEmailDelivery
{
    private readonly PasswordResetOptions _options = passwordResetOptions.Value;

    public Task DeliverAsync(
        string email,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var separator = _options.ResetUrl.Contains('?') ? '&' : '?';
        var actionUrl =
            $"{_options.ResetUrl}{separator}token={Uri.EscapeDataString(plainTextToken)}";

        var encodedEmail = HtmlEncoder.Default.Encode(email);
        var encodedActionUrl = HtmlEncoder.Default.Encode(actionUrl);
        var encodedExpiresAt = HtmlEncoder.Default.Encode(expiresAt.ToString("O"));

        var message = new EmailMessage(
            To: [new EmailRecipient(email)],
            Subject: "AgriDrone System Manager invitation",
            HtmlBody: $"""
                <h2>AgriDrone System Manager invitation</h2>
                <p>Hello <strong>{encodedEmail}</strong>,</p>
                <p>
                    A System Administrator has invited you to join AgriDrone
                    as a System Manager.
                </p>
                <p><a href="{encodedActionUrl}">Set your password</a></p>
                <p>This invitation expires at {encodedExpiresAt}.</p>
                <p>
                    After setting your password, sign in and complete your
                    personal profile. Operational access will be enabled after
                    your profile and flight qualification are activated by a
                    System Administrator.
                </p>
                """,
            TextBody:
                $"Hello {email},{Environment.NewLine}" +
                "A System Administrator has invited you to join AgriDrone " +
                $"as a System Manager.{Environment.NewLine}" +
                $"Set your password: {actionUrl}{Environment.NewLine}" +
                $"This invitation expires at {expiresAt:O}.",
            MessageId: Guid.NewGuid().ToString("D"));

        return emailSender.SendAsync(message, cancellationToken);
    }
}
