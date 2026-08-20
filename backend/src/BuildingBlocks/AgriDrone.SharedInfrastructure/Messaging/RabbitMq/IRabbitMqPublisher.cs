namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal interface IRabbitMqPublisher
{
    Task PublishAsync(
        RabbitMqPublishMessage message,
        CancellationToken cancellationToken);
}
