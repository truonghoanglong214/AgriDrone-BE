using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal interface IEmailTemplate
{
    string Key { get; }

    EmailMessage Render(
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId);
}
