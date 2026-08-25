using System.Text.Encodings.Web;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;

internal sealed class PasswordResetEmailDelivery(
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> passwordResetOptions)
    : IPasswordResetEmailDelivery
{
    private readonly PasswordResetOptions _options = passwordResetOptions.Value;

    public Task DeliverAsync(
        string email,
        string fullName,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var resetUrl = BuildResetUrl(plainTextToken);
        var encodedFullName = HtmlEncoder.Default.Encode(fullName);
        var encodedResetUrl = HtmlEncoder.Default.Encode(resetUrl);

        var message = new EmailMessage(
            To: [new EmailRecipient(email, fullName)],
            Subject: "Reset your AgriDrone password",
            HtmlBody: $"""
                <h2>Reset your password</h2>
                <p>Hello {encodedFullName},</p>
                <p>We received a request to set or reset your AgriDrone password.</p>
                <p><a href="{encodedResetUrl}">Set password</a></p>
                <p>This link expires at {expiresAt:O}. If you did not request it, you can ignore this email.</p>
                """,
            TextBody:
                $"Hello {fullName},{Environment.NewLine}" +
                $"Set or reset your AgriDrone password: {resetUrl}{Environment.NewLine}" +
                $"This link expires at {expiresAt:O}. If you did not request it, you can ignore this email.");

        return emailSender.SendAsync(message, cancellationToken);
    }

    private string BuildResetUrl(string plainTextToken)
    {
        var separator = _options.ResetUrl.Contains('?') ? '&' : '?';

        return $"{_options.ResetUrl}{separator}token={Uri.EscapeDataString(plainTextToken)}";
    }
}
