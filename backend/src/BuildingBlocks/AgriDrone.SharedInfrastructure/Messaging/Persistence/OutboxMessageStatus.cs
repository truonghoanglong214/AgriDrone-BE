namespace AgriDrone.SharedInfrastructure.Messaging.Persistence;

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Retry = 3,
    Published = 4,
    Dead = 5
}
