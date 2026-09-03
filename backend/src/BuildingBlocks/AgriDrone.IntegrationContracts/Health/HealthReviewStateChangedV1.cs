namespace AgriDrone.IntegrationContracts.Health;

public sealed record HealthReviewStateChangedV1(
    Guid HandoffId,
    Guid MissionId,
    Guid FarmId,
    Guid ZoneId,
    long ReviewVersion,
    string State,
    int TotalObservations,
    int PendingReviews,
    int AwaitingFieldVerification,
    int ResolvedReviews,
    DateTimeOffset ChangedAt);