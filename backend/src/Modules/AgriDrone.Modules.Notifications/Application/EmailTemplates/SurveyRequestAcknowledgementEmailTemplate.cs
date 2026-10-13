using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class SurveyRequestAcknowledgementEmailTemplate
    : IEmailTemplate
{
    public string Key => EmailTemplateKeys.SurveyRequestAcknowledgement;

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

        var encodedApplicantName = HtmlEncoder.Default.Encode(applicantName);
        var encodedRequestNumber = HtmlEncoder.Default.Encode(requestNumber);
        var encodedServiceName = HtmlEncoder.Default.Encode(serviceName);
        var subjectRequestNumber = EmailTemplateVariables.ForSubject(
            requestNumber);

        return new EmailMessage(
            To: recipients,
            Subject: $"AgriDrone received request {subjectRequestNumber}",
            HtmlBody: $"""
            <h2>Survey request received</h2>
            <p>Hello <strong>{encodedApplicantName}</strong>,</p>
            <p>
                We received request <strong>{encodedRequestNumber}</strong>
                for <strong>{encodedServiceName}</strong>.
            </p>
            <p>Its current status is Submitted. Our team will review it.</p>
            """,
            TextBody:
                $"Hello {applicantName},{Environment.NewLine}" +
                $"We received request {requestNumber} for {serviceName}." +
                Environment.NewLine +
                "Its current status is Submitted. Our team will review it.",
            MessageId: messageId);
    }
}
