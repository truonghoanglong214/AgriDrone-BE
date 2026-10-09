namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct RequestIdempotency
{
    public const int MaximumCallerScopeLength = 100;
    public const int MaximumKeyLength = 100;

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
        if (normalizedScope.Length > MaximumCallerScopeLength)
        {
            throw new ArgumentException(
                $"Idempotency caller scope cannot exceed {MaximumCallerScopeLength} characters.",
                nameof(callerScope));
        }

        if (normalizedKey.Length > MaximumKeyLength)
        {
            throw new ArgumentException(
                $"Idempotency key cannot exceed {MaximumKeyLength} characters.",
                nameof(key));
        }

        return new RequestIdempotency(normalizedScope, normalizedKey);
    }
}
