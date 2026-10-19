using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum BaselineRoutePlanReferenceStatus
{
    Generated,
    Approved,
    Rejected,
    Superseded
}

/// <summary>
/// BE1 reference to a BE2-owned automatic route plan. Waypoints remain owned by
/// BE2; BE1 records the boundary/session binding and approval safety result.
/// </summary>
public sealed class BaselineRoutePlanReference : Entity
{
    private BaselineRoutePlanReference() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid BoundaryCaptureSessionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid RoutePlanId { get; private set; }
    public int RoutePlanVersion { get; private set; }
    public decimal SafetyInsetMeters { get; private set; }
    public bool IsWithinSafetyInset { get; private set; }
    public BaselineRoutePlanReferenceStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static BaselineRoutePlanReference RecordGenerated(
        Guid surveyOrderId,
        Guid boundaryCaptureSessionId,
        Guid farmBoundaryVersionId,
        Guid routePlanId,
        int routePlanVersion,
        decimal safetyInsetMeters,
        bool isWithinSafetyInset,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(boundaryCaptureSessionId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(routePlanId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(routePlanVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(safetyInsetMeters);
        DomainGuard.Utc(createdAt);
        return new BaselineRoutePlanReference
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            BoundaryCaptureSessionId = boundaryCaptureSessionId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            RoutePlanId = routePlanId,
            RoutePlanVersion = routePlanVersion,
            SafetyInsetMeters = safetyInsetMeters,
            IsWithinSafetyInset = isWithinSafetyInset,
            Status = BaselineRoutePlanReferenceStatus.Generated,
            CreatedAt = createdAt
        };
    }

    public void Approve(Guid approvedBy, DateTimeOffset approvedAt)
    {
        DomainGuard.NotEmpty(approvedBy);
        DomainGuard.Utc(approvedAt);
        if (Status != BaselineRoutePlanReferenceStatus.Generated)
        {
            throw new InvalidOperationException(
                "Only a generated route-plan reference can be approved.");
        }

        if (!IsWithinSafetyInset)
        {
            throw new InvalidOperationException(
                "A route outside the safety inset cannot be approved.");
        }

        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        Status = BaselineRoutePlanReferenceStatus.Approved;
    }
}
