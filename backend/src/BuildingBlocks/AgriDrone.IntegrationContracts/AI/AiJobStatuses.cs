namespace AgriDrone.IntegrationContracts.AI;

public static class AiJobStatuses
{
    public const string Accepted = "ACCEPTED";

    public const string Processing = "PROCESSING";

    public const string Completed = "COMPLETED";

    public const string Failed = "FAILED";

    public const string Cancelled = "CANCELLED";

    public static bool IsSupported(string? status) =>
        status is
            Accepted or
            Processing or
            Completed or
            Failed or
            Cancelled;

    public static bool IsTerminal(string? status) =>
        status is Completed or Failed or Cancelled;
}