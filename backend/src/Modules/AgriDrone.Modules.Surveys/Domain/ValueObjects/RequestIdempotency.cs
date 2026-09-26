namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct RequestIdempotency
{
    private RequestIdempotency(string callerScope, string key)
    {
        CallerScope = callerScope;
        Key = key;
    }

    public string CallerScope { get; }
    public string Key { get; }

    public static RequestIdempotency Create(string callerScope, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalizedScope = callerScope.Trim().ToLowerInvariant();
        var normalizedKey = key.Trim();
        if (normalizedScope.Length > 100 || normalizedKey.Length > 200)
        {
            throw new ArgumentException(
                "Idempotency caller scope or key exceeds the supported length.");
        }

        return new RequestIdempotency(normalizedScope, normalizedKey);
    }
}
