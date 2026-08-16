namespace AgriDrone.SharedKernel.Application.Abstractions.Notifications;

public sealed record EmailMessage(
    IReadOnlyCollection<EmailRecipient> To,
    string Subject,
    string? HtmlBody = null,
    string? TextBody = null,
    IReadOnlyCollection<EmailRecipient>? Cc = null,
    IReadOnlyCollection<EmailRecipient>? Bcc = null,
    EmailRecipient? ReplyTo = null);
