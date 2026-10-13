using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

internal static class SurveyTransitionGuard
{
    public static void EnsureAllowed<TState>(
        bool allowed,
        TState from,
        TState to,
        string errorCode)
        where TState : struct, Enum
    {
        if (!allowed)
        {
            throw new SurveyDomainException(
                errorCode,
                $"Transition from '{from}' to '{to}' is not allowed.");
        }
    }

    public static void EnsureTimestamp(
        DateTimeOffset occurredAt,
        DateTimeOffset updatedAt)
    {
        DomainGuard.Utc(occurredAt);
        if (updatedAt != default && occurredAt < updatedAt)
        {
            throw new ArgumentException(
                "Transition timestamp cannot be earlier than the last update.",
                nameof(occurredAt));
        }
    }

    public static SurveyDomainException VersionConflict(
        string errorCode,
        uint expected,
        uint actual) =>
        new(
            errorCode,
            $"Expected version '{expected}', but the current version is '{actual}'.");
}
