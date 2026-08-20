namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

public enum IntegrationMessageDisposition
{
    Acknowledge = 1,
    Retry = 2,
    DeadLetter = 3
}
