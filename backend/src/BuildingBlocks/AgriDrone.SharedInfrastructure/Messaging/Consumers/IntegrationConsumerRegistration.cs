namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

internal sealed record IntegrationConsumerRegistration(
    string ConsumerName,
    Type ProcessorType);
