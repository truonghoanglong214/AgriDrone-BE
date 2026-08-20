namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal static class RabbitMqHeaders
{
    public const string RetryCount = "x-agridrone-retry-count";

    public const string ErrorCode = "x-agridrone-error-code";

    public const string Error = "x-agridrone-error";

    public const string FailedAt = "x-agridrone-failed-at";

    public const string OriginalExchange = "x-agridrone-original-exchange";

    public const string OriginalRoutingKey =
        "x-agridrone-original-routing-key";
}
