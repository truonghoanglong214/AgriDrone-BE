using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Notifications.Application.EmailTemplates;
using AgriDrone.Modules.Notifications.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;

namespace AgriDrone.Modules.Notifications.Infrastructure.Messaging;

internal sealed class EmailNotificationRequestedHandler(
    NotificationsDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator,
    EmailTemplateRenderer templateRenderer,
    IEmailSender emailSender)
    : IIntegrationMessageHandler<EmailNotificationRequestedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<EmailNotificationRequestedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.NotificationsEmailV1,
            envelope,
            (_, token) => DeliverAsync(envelope, token),
            cancellationToken);

    private async Task<InboxHandlerResult> DeliverAsync(
        IntegrationEventEnvelope<EmailNotificationRequestedV1> envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipients = envelope.Payload.Recipients
                .Select(recipient => new EmailRecipient(
                    recipient.Address,
                    recipient.DisplayName))
                .ToArray();
            var message = templateRenderer.Render(
                envelope.Payload.TemplateKey,
                recipients,
                envelope.Payload.Variables,
                $"{envelope.Payload.NotificationId:D}@notifications.agridrone");

            await emailSender.SendAsync(message, cancellationToken);

            return InboxHandlerResult.Completed(
                JsonSerializer.Serialize(new
                {
                    envelope.Payload.NotificationId,
                    envelope.Payload.TemplateKey
                }));
        }
        catch (EmailTemplateException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                exception.Code,
                exception.Message);
        }
    }
}
