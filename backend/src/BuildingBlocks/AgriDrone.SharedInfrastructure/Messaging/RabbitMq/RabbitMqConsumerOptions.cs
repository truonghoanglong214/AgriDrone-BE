namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConsumerOptions
{
    public string Name { get; set; } = string.Empty;

    public string QueueName { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;
}
