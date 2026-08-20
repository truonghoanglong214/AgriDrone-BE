namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

public sealed record IntegrationMessageProcessingResult(
    IntegrationMessageDisposition Disposition,
    string? ErrorCode = null,
    string? Error = null)
{
    public static IntegrationMessageProcessingResult Acknowledge() =>
        new(IntegrationMessageDisposition.Acknowledge);

    public static IntegrationMessageProcessingResult Retry(
        string? error = null) =>
        new(IntegrationMessageDisposition.Retry, Error: error);

    public static IntegrationMessageProcessingResult DeadLetter(
        string errorCode,
        string? error = null) =>
        new(
            IntegrationMessageDisposition.DeadLetter,
            errorCode,
            error);
}
