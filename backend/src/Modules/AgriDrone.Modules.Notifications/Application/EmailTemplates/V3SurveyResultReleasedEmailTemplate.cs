using System.Text.Encodings.Web;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Application.EmailTemplates;

internal sealed class V3SurveyResultReleasedEmailTemplate : IEmailTemplate
{
    public string Key => EmailTemplateKeys.V3SurveyResultReleased;

    public EmailMessage Render(
        IReadOnlyCollection<EmailRecipient> recipients,
        IReadOnlyDictionary<string, string> variables,
        string messageId)
    {
        var orderNumber = EmailTemplateVariables.Require(variables, EmailTemplateVariableKeys.OrderNumber);
        const string instruction = "Đăng nhập AgriDrone và mở chi tiết đơn để xem kết quả và thời hạn khiếu nại.";
        return new EmailMessage(
            To: recipients,
            Subject: $"AgriDrone đã mở kết quả đơn {EmailTemplateVariables.ForSubject(orderNumber)}",
            HtmlBody: $"<p>Kết quả đơn <strong>{HtmlEncoder.Default.Encode(orderNumber)}</strong> đã được mở.</p><p>{instruction}</p>",
            TextBody: $"Kết quả đơn {orderNumber} đã được mở. {instruction}",
            MessageId: messageId);
    }
}
