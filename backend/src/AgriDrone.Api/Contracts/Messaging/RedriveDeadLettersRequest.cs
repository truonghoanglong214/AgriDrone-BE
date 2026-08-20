namespace AgriDrone.Api.Contracts.Messaging;

public sealed record RedriveDeadLettersRequest(int MaximumMessages = 10);
