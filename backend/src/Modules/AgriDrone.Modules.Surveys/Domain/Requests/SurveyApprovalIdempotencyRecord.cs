using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyApprovalIdempotencyRecord : Entity
{
    public const int MaximumKeyLength = 100;
    public const int CommandFingerprintLength = 64;

    private SurveyApprovalIdempotencyRecord() { }

    public Guid SurveyRequestId { get; private set; }
    public Guid ActorId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string CommandFingerprint { get; private set; } = null!;
    public JsonDocument? ResultSnapshot { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static SurveyApprovalIdempotencyRecord Claim(
        Guid surveyRequestId,
        Guid actorId,
        string idempotencyKey,
        string commandFingerprint,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyRequestId);
        DomainGuard.NotEmpty(actorId);
        DomainGuard.Utc(createdAt);

        var normalizedKey = NormalizeRequired(
            idempotencyKey,
            MaximumKeyLength,
            nameof(idempotencyKey));
        var normalizedFingerprint = NormalizeRequired(
                commandFingerprint,
                CommandFingerprintLength,
                nameof(commandFingerprint))
            .ToUpperInvariant();
        if (normalizedFingerprint.Length != CommandFingerprintLength ||
            normalizedFingerprint.Any(character =>
                !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Command fingerprint must be a 64-character hexadecimal SHA-256 value.",
                nameof(commandFingerprint));
        }

        return new SurveyApprovalIdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            SurveyRequestId = surveyRequestId,
            ActorId = actorId,
            IdempotencyKey = normalizedKey,
            CommandFingerprint = normalizedFingerprint,
            CreatedAt = createdAt
        };
    }

    public void Complete(
        JsonDocument resultSnapshot,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(resultSnapshot);
        DomainGuard.Utc(completedAt);
        if (completedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAt),
                "Completion cannot precede the idempotency claim.");
        }

        if (ResultSnapshot is not null || CompletedAt.HasValue)
        {
            throw new InvalidOperationException(
                "The approval idempotency record is already complete.");
        }

        ResultSnapshot = JsonDocument.Parse(
            resultSnapshot.RootElement.GetRawText());
        CompletedAt = completedAt;
    }

    public bool Matches(
        Guid surveyRequestId,
        Guid actorId,
        string idempotencyKey,
        string commandFingerprint) =>
        SurveyRequestId == surveyRequestId &&
        ActorId == actorId &&
        string.Equals(
            IdempotencyKey,
            idempotencyKey.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(
            CommandFingerprint,
            commandFingerprint,
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
