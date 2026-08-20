namespace AgriDrone.SharedInfrastructure.Messaging.Inbox;

public enum InboxHandlerDisposition
{
    Completed = 1,
    Retry = 2,
    PermanentFailure = 3
}
