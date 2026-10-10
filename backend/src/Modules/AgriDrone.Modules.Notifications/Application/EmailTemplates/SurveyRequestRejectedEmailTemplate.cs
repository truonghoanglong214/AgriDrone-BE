using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class SurveyRequestRejectedEmailTemplate : IEmailTemplate
{
    public string Key => EmailTemplateKeys.SurveyRequestRejected;

    public EmailMessage Render(
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId)
    {
        var applicantName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.ApplicantName);
        var requestNumber = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RequestNumber);
        var serviceName = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.SurveyServiceName);
        var rejectionReason = EmailTemplateVariables.Require(
            variables,
            EmailTemplateVariableKeys.RejectionReason);

        var encodedApplicantName = HtmlEncoder.Default.Encode(applicantName);
        var encodedRequestNumber = HtmlEncoder.Default.Encode(requestNumber);
        var encodedServiceName = HtmlEncoder.Default.Encode(serviceName);
        var encodedReason = HtmlEncoder.Default.Encode(rejectionReason);
        var subjectRequestNumber = EmailTemplateVariables.ForSubject(
            requestNumber);

        return new EmailMessage(
            To: recipients,
            Subject: $"AgriDrone request {subjectRequestNumber} was not approved",
            HtmlBody: $"""
            <h2>Survey request update</h2>
            <p>Hello <strong>{encodedApplicantName}</strong>,</p>
            <p>
                Request <strong>{encodedRequestNumber}</strong> for
                <strong>{encodedServiceName}</strong> was not approved.
            </p>
            <p><strong>Reason:</strong> {encodedReason}</p>
            """,
            TextBody:
                $"Hello {applicantName},{Environment.NewLine}" +
                $"Request {requestNumber} for {serviceName} was not approved." +
                Environment.NewLine +
                $"Reason: {rejectionReason}",
            MessageId: messageId);
    }
}
