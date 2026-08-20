namespace AgriDrone.SharedInfrastructure.Messaging.Persistence;

internal static class MessagingPersistenceLimits
{
    public const int MaximumConsumerNameLength = 150;

    public const int MaximumRoutingKeyLength = 200;

    public const int MaximumContentTypeLength = 100;

    public const int MaximumPartitionKeyLength = 200;

    public const int MaximumErrorCodeLength = 200;

    public const int MaximumErrorLength = 2_000;
}
