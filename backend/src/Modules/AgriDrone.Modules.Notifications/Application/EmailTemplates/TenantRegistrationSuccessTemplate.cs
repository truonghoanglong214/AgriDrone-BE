using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class TenantRegistrationSuccessTemplate : IEmailTemplate
{
    public string Key => EmailTemplateKeys.TenantRegistrationSuccess;

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
        var tenantCode = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.TenantCode);
        var roleName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RoleName);
        var registeredAt = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RegisteredAt);
        var loginUrl = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.LoginUrl);

        var encodedUserName = HtmlEncoder.Default.Encode(userName);
        var encodedTenantName = HtmlEncoder.Default.Encode(tenantName);
        var encodedTenantCode = HtmlEncoder.Default.Encode(tenantCode);
        var encodedRoleName = HtmlEncoder.Default.Encode(roleName);
        var encodedRegisteredAt = HtmlEncoder.Default.Encode(registeredAt);
        var encodedLoginUrl = HtmlEncoder.Default.Encode(loginUrl);
        var subjectTenantName = EmailTemplateVariables.ForSubject(tenantName);

        return new EmailMessage(
            To: recipients,
            Subject:
                $"Welcome to AgriDrone. {subjectTenantName} has been created!",
            HtmlBody: $"""
                <h2>Welcome to AgriDrone</h2>
                <p>Dear {encodedUserName},</p>
                <p>
                    Your account and tenant
                    <strong>{encodedTenantName}</strong>
                    (Code: <strong>{encodedTenantCode}</strong>)
                    have been created successfully.
                </p>
                <p>Your role is: <strong>{encodedRoleName}</strong>.</p>
                <p>Registered at: {encodedRegisteredAt}</p>
                <p><a href="{encodedLoginUrl}">Log in to AgriDrone</a></p>
                <p>Thank you for joining us!</p>
                """,
            TextBody:
                $"Hello {userName},{Environment.NewLine}" +
                $"Your account and tenant {tenantName} ({tenantCode}) " +
                $"have been created successfully.{Environment.NewLine}" +
                $"Role: {roleName}{Environment.NewLine}" +
                $"Registered at: {registeredAt}{Environment.NewLine}" +
                $"Log in: {loginUrl}",
            MessageId: messageId);
    }
}
