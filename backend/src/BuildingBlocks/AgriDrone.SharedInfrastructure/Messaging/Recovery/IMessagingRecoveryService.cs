namespace AgriDrone.SharedInfrastructure.Messaging.Recovery;

public interface IMessagingRecoveryService
{
    Task<bool> RedriveOutboxAsync(
        Guid messageId,
        Guid actorId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<int> RedriveDeadLettersAsync(
        string consumerName,
        int maximumMessages,
        Guid actorId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
