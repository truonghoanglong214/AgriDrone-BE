using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class EmailTemplateRenderer(
    IEnumerable<IEmailTemplate> templates)
{
    private readonly Dictionary<string, IEmailTemplate> _templates =
        templates.ToDictionary(
            template => template.Key,
            StringComparer.OrdinalIgnoreCase);

    public EmailMessage Render(
        string templateKey,
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId)
    {
        if (!_templates.TryGetValue(templateKey, out var template))
        {
            throw new EmailTemplateException(
                "EMAIL_TEMPLATE_NOT_FOUND",
                $"Email template '{templateKey}' is not registered.");
        }

        return template.Render(recipients, variables, messageId);
    }
}
