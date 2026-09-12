using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class TenantWelcomeEmailTemplate : IEmailTemplate
{
    public string Key => EmailTemplateKeys.TenantWelcome;

    public EmailMessage Render(
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId)
    {
        var userName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.UserName);
        var tenantName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.TenantName);
        var roleName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RoleName);

        var encodedUserName = HtmlEncoder.Default.Encode(userName);
        var encodedTenantName = HtmlEncoder.Default.Encode(tenantName);
        var encodedRoleName = HtmlEncoder.Default.Encode(roleName);
        var subjectTenantName = EmailTemplateVariables.ForSubject(tenantName);

        return new EmailMessage(
            To: recipients,
            Subject: $"Welcome to {subjectTenantName}",
            HtmlBody: $"""
            <h2>Welcome to AgriDrone</h2>
            <p>Hello <strong>{encodedUserName}</strong>,</p>
            <p>
                Your account is ready in
                <strong>{encodedTenantName}</strong>
                with the role <strong>{encodedRoleName}</strong>.
            </p>
            """,
            TextBody:
                $"Hello {userName},{Environment.NewLine}" +
                $"Your account is ready in {tenantName} " +
                $"with the role {roleName}.",
            MessageId: messageId);
    }
}
