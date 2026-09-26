using System.Text.Encodings.Web;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;

internal sealed class SystemManagerInvitationEmailDelivery(
    IEmailSender emailSender,
    IOptions<SystemManagerInvitationOptions> invitationOptions)
    : ISystemManagerInvitationEmailDelivery
{
    private readonly SystemManagerInvitationOptions _options =
        invitationOptions.Value;

    public Task DeliverAsync(
        string email,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var separator = _options.AcceptUrl.Contains('?') ? '&' : '?';

        var actionUrl =
            $"{_options.AcceptUrl}{separator}token=" +
            Uri.EscapeDataString(plainTextToken);

        var encodedEmail = HtmlEncoder.Default.Encode(email);
        var encodedUrl = HtmlEncoder.Default.Encode(actionUrl);
        var encodedExpiry =
            HtmlEncoder.Default.Encode(expiresAt.ToString("O"));

        var message = new EmailMessage(
            To: [new EmailRecipient(email)],
            Subject: "AgriDrone System Manager invitation",
            HtmlBody: $"""
                <h2>AgriDrone System Manager invitation</h2>
                <p>Hello <strong>{encodedEmail}</strong>,</p>
                <p>
                    A System Administrator invited you to join AgriDrone
                    as a System Manager.
                </p>
                <p>
                    <a href="{encodedUrl}">Accept invitation</a>
                </p>
                <p>This invitation expires at {encodedExpiry}.</p>
                """,
            TextBody:
                $"Hello {email},{Environment.NewLine}" +
                "A System Administrator invited you to join AgriDrone " +
                $"as a System Manager.{Environment.NewLine}" +
                $"Accept invitation: {actionUrl}{Environment.NewLine}" +
                $"Expires at: {expiresAt:O}",
            MessageId: Guid.NewGuid().ToString("D"));

        return emailSender.SendAsync(message, cancellationToken);
    }
}
