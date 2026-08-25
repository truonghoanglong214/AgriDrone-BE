namespace AgriDrone.IntegrationContracts.Health;

public static class HealthReviewStates
{
    public const string Pending = "PENDING";

    public const string AwaitingFieldVerification =
        "AWAITING_FIELD_VERIFICATION";

    public const string Resolved = "RESOLVED";

    public static bool IsSupported(string? state) =>
        state is Pending or AwaitingFieldVerification or Resolved;
}