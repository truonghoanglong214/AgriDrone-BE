namespace AgriDrone.IntegrationContracts.Notifications;

public sealed record EmailNotificationRequestedV1(
    Guid NotificationId,
    string TemplateKey,
    IReadOnlyList<EmailRecipientV1> Recipients,
    IReadOnlyDictionary<string, string> Variables);
