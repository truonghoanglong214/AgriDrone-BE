using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum ExtraFlightReason { InsufficientData, ApprovedScopeChange }
public enum ExtraFlightRequestStatus { Pending, Approved, Rejected, Consumed }

public sealed class ExtraFlightRequest : Entity
{
    private ExtraFlightRequest() { }
    public Guid SurveyOrderId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? SourceMissionId { get; private set; }
    public Guid? ExtraMissionId { get; private set; }
    public ExtraFlightReason Reason { get; private set; }
    public string EvidenceReference { get; private set; } = null!;
    public decimal CostImpact { get; private set; }
    public string Currency { get; private set; } = null!;
    public ExtraFlightRequestStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static ExtraFlightRequest Create(
        Guid surveyOrderId,
        Guid farmId,
        Guid? sourceMissionId,
        ExtraFlightReason reason,
        string evidenceReference,
        decimal costImpact,
        CurrencyCode currency,
        Guid requestedBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(requestedBy);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        ArgumentOutOfRangeException.ThrowIfNegative(costImpact);
        return new ExtraFlightRequest
        {
            Id = Guid.CreateVersion7(), SurveyOrderId = surveyOrderId, FarmId = farmId,
            SourceMissionId = sourceMissionId, Reason = reason,
            EvidenceReference = evidenceReference.Trim(), CostImpact = costImpact,
            Currency = currency.Value, Status = ExtraFlightRequestStatus.Pending,
            RequestedBy = requestedBy, CreatedAt = createdAt
        };
    }

    public void Review(bool approved, Guid reviewedBy, DateTimeOffset reviewedAt, uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Extra-flight request version conflict.");
        if (Status != ExtraFlightRequestStatus.Pending) throw new InvalidOperationException("Only pending requests can be reviewed.");
        DomainGuard.NotEmpty(reviewedBy); DomainGuard.Utc(reviewedAt);
        Status = approved ? ExtraFlightRequestStatus.Approved : ExtraFlightRequestStatus.Rejected;
        ReviewedBy = reviewedBy; ReviewedAt = reviewedAt; Version++;
    }

    public void Consume(Guid extraMissionId, DateTimeOffset consumedAt, uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Extra-flight request version conflict.");
        if (Status != ExtraFlightRequestStatus.Approved) throw new InvalidOperationException("Extra flight requires approval.");
        DomainGuard.NotEmpty(extraMissionId); DomainGuard.Utc(consumedAt);
        ExtraMissionId = extraMissionId; Status = ExtraFlightRequestStatus.Consumed; Version++;
    }
}
