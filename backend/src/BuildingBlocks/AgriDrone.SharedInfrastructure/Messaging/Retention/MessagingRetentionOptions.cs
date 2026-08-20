namespace AgriDrone.SharedInfrastructure.Messaging.Retention;

public sealed class MessagingRetentionOptions
{
    public const string SectionName = "Messaging:Retention";

    public bool Enabled { get; set; } = true;

    public int CleanupIntervalHours { get; set; } = 24;

    public int CompletedInboxRetentionDays { get; set; } = 30;

    public int PublishedOutboxRetentionDays { get; set; } = 30;

    public int BatchSize { get; set; } = 1_000;
}
