using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class TenantInvitationEmailTemplate : IEmailTemplate
{
    public string Key => EmailTemplateKeys.TenantInvitation;

    public EmailMessage Render(
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId)
    {
        var tenantName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.TenantName);
        var roleName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RoleName);
        var actionUrl = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.ActionUrl);
        var expiresAt = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.ExpiresAt);

        var encodedTenantName = HtmlEncoder.Default.Encode(tenantName);
        var encodedRoleName = HtmlEncoder.Default.Encode(roleName);
        var encodedActionUrl = HtmlEncoder.Default.Encode(actionUrl);
        var encodedExpiresAt = HtmlEncoder.Default.Encode(expiresAt);
        var subjectTenantName = EmailTemplateVariables.ForSubject(tenantName);
        var subjectRoleName = EmailTemplateVariables.ForSubject(roleName);

        return new EmailMessage(
            To: recipients,
            Subject:
                $"Invitation to join {subjectTenantName} as {subjectRoleName}",
            HtmlBody: $"""
            <h2>AgriDrone tenant invitation</h2>
            <p>
                You have been invited to join
                <strong>{encodedTenantName}</strong>
                as <strong>{encodedRoleName}</strong>.
            </p>
            <p><a href="{encodedActionUrl}">Accept invitation</a></p>
            <p>This invitation expires at {encodedExpiresAt}.</p>
            """,
            TextBody:
                $"You have been invited to join {tenantName} as {roleName}." +
                $"{Environment.NewLine}Accept the invitation: {actionUrl}" +
                $"{Environment.NewLine}Expires at: {expiresAt}",
            MessageId: messageId);
    }
}
