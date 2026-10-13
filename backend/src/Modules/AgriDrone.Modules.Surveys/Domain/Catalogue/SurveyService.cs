using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyService : AggregateRoot
{
    private SurveyService() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public SurveyServiceType ServiceType { get; private set; }
    public SurveyServiceStatus Status { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<SurveyServicePrice> Prices { get; private set; } = [];

    public static SurveyService Create(
        string code,
        string name,
        string description,
        SurveyServiceType serviceType,
        DateTimeOffset createdAt)
    {
        var normalizedCode = NormalizeRequired(code, 50, nameof(code))
            .ToUpperInvariant();
        var normalizedName = NormalizeRequired(name, 150, nameof(name));
        var normalizedDescription = NormalizeRequired(
            description,
            4000,
            nameof(description));
        if (!Enum.IsDefined(serviceType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceType),
                serviceType,
                "Survey service type is not supported.");
        }

        DomainGuard.Utc(createdAt);
        return new SurveyService
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = normalizedName,
            Description = normalizedDescription,
            ServiceType = serviceType,
            Status = SurveyServiceStatus.Experimental,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Activate(DateTimeOffset activatedAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureTransition(
            Status == SurveyServiceStatus.Experimental,
            SurveyServiceStatus.Active);
        Touch(activatedAt);
        Status = SurveyServiceStatus.Active;
    }

    public void MarkExperimental(
        DateTimeOffset changedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureTransition(
            Status == SurveyServiceStatus.Active,
            SurveyServiceStatus.Experimental);
        Touch(changedAt);
        Status = SurveyServiceStatus.Experimental;
    }

    public void Retire(DateTimeOffset retiredAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        EnsureTransition(
            Status is SurveyServiceStatus.Active or
                SurveyServiceStatus.Experimental,
            SurveyServiceStatus.Retired);
        Touch(retiredAt);
        Status = SurveyServiceStatus.Retired;
    }

    public void UpdateMetadata(
        string name,
        string description,
        DateTimeOffset updatedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status == SurveyServiceStatus.Retired)
        {
            throw new SurveyDomainException(
                SurveyServiceDomainErrorCodes.RetiredServiceImmutable,
                "A retired survey service cannot be changed.");
        }

        Name = NormalizeRequired(name, 150, nameof(name));
        Description = NormalizeRequired(
            description,
            4000,
            nameof(description));
        Touch(updatedAt);
    }

    public void AddPriceVersion(
        SurveyServicePrice price,
        DateTimeOffset changedAt,
        uint expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(price);
        EnsureVersion(expectedVersion);
        if (Status == SurveyServiceStatus.Retired)
        {
            throw new SurveyDomainException(
                SurveyServiceDomainErrorCodes.RetiredServiceImmutable,
                "A retired survey service cannot receive a new price.");
        }

        if (price.SurveyServiceId != Id)
        {
            throw new ArgumentException(
                "Price must belong to this survey service.",
                nameof(price));
        }

        Touch(changedAt);
        Prices.Add(price);
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyServiceDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    private void EnsureTransition(
        bool allowed,
        SurveyServiceStatus target) =>
        SurveyTransitionGuard.EnsureAllowed(
            allowed,
            Status,
            target,
            SurveyServiceDomainErrorCodes.InvalidLifecycleTransition);

    private void Touch(DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        UpdatedAt = occurredAt;
    }

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
