using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AgriDrone.Integrations.Email;

internal sealed partial class SmtpEmailSender(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpEmailOptions _options = options.Value;

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                $"SMTP email is disabled. Enable '{SmtpEmailOptions.SectionName}:Enabled' before sending email.");
        }

        ValidateMessage(message);

        var recipientCount = CountRecipients(message);
        var mimeMessage = CreateMimeMessage(message);

        using var client = new SmtpClient
        {
            Timeout = checked(_options.TimeoutSeconds * 1_000)
        };

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                MapSecurityMode(_options.SecurityMode),
                cancellationToken);

            var username = _options.Username;
            var password = _options.Password;

            if (!string.IsNullOrWhiteSpace(username) &&
                !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(
                    username,
                    password,
                    cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                LogEmailSent(logger, recipientCount);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    private MimeMessage CreateMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        AddRecipients(mimeMessage.To, message.To);
        AddRecipients(mimeMessage.Cc, message.Cc);
        AddRecipients(mimeMessage.Bcc, message.Bcc);

        if (message.ReplyTo is not null)
        {
            mimeMessage.ReplyTo.Add(ToMailboxAddress(message.ReplyTo));
        }

        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        return mimeMessage;
    }

    private static void AddRecipients(
        InternetAddressList target,
        IReadOnlyCollection<EmailRecipient>? recipients)
    {
        if (recipients is null)
        {
            return;
        }

        foreach (var recipient in recipients)
        {
            target.Add(ToMailboxAddress(recipient));
        }
    }

    private static MailboxAddress ToMailboxAddress(EmailRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);

        if (!MailboxAddress.TryParse(recipient.Address, out var mailboxAddress))
        {
            throw new ArgumentException(
                $"'{recipient.Address}' is not a valid email address.",
                nameof(recipient));
        }

        return string.IsNullOrWhiteSpace(recipient.DisplayName)
            ? mailboxAddress
            : new MailboxAddress(recipient.DisplayName, mailboxAddress.Address);
    }

    private static void ValidateMessage(EmailMessage message)
    {
        if (message.To is null || message.To.Count == 0)
        {
            throw new ArgumentException(
                "At least one To recipient is required.",
                nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new ArgumentException(
                "Email subject is required.",
                nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.HtmlBody) &&
            string.IsNullOrWhiteSpace(message.TextBody))
        {
            throw new ArgumentException(
                "Either an HTML body or a text body is required.",
                nameof(message));
        }
    }

    private static int CountRecipients(EmailMessage message) =>
        message.To.Count + (message.Cc?.Count ?? 0) + (message.Bcc?.Count ?? 0);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Email sent successfully to {RecipientCount} recipient(s).")]
    private static partial void LogEmailSent(ILogger logger, int recipientCount);

    private static SecureSocketOptions MapSecurityMode(SmtpSecurityMode securityMode) =>
        securityMode switch
        {
            SmtpSecurityMode.Auto => SecureSocketOptions.Auto,
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new ArgumentOutOfRangeException(
                nameof(securityMode),
                securityMode,
                "Unsupported SMTP security mode.")
        };
}
