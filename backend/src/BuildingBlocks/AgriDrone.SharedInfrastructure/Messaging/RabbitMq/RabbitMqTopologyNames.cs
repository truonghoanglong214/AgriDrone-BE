namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal static class RabbitMqTopologyNames
{
    public static string RetryQueue(
        RabbitMqConsumerOptions consumer,
        int retryIndex) =>
        $"{consumer.QueueName}.retry.{retryIndex + 1}";

    public static string RetryRoutingKey(
        RabbitMqConsumerOptions consumer,
        int retryIndex) =>
        $"{consumer.Name}.retry.{retryIndex + 1}";

    public static string DeadLetterQueue(
        RabbitMqConsumerOptions consumer) =>
        $"{consumer.QueueName}.dead";

    public static string DeadLetterRoutingKey(
        RabbitMqConsumerOptions consumer) =>
        $"{consumer.RoutingKey}.dead";
}
