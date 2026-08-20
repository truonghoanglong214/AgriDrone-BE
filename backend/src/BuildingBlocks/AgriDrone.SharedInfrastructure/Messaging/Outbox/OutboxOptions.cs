namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Messaging:Outbox";

    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 50;

    public int PollIntervalMilliseconds { get; set; } = 1000;

    public int LeaseSeconds { get; set; } = 30;

    public int MaximumAttempts { get; set; } = 10;

    public int RetryBaseSeconds { get; set; } = 5;

    public int RetryMaximumSeconds { get; set; } = 300;
}
