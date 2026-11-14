using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRequestTracking
{
    private SurveyRequestTracking() { }

    public Guid SurveyRequestId { get; private set; }
    public string TrackingTokenHash { get; private set; } = null!;
    public string EncryptedToken { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyRequestTracking Create(
        Guid surveyRequestId,
        string trackingTokenHash,
        string encryptedToken,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyRequestId, nameof(surveyRequestId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedHash = PolicyValidationGuard.NormalizeRequired(trackingTokenHash, 64, nameof(trackingTokenHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedToken);

        return new SurveyRequestTracking
        {
            SurveyRequestId = surveyRequestId,
            TrackingTokenHash = normalizedHash,
            EncryptedToken = encryptedToken.Trim(),
            CreatedAt = createdAt
        };
    }
}
