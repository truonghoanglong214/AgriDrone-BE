using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class FarmBaseMapVersion : AggregateRoot
{
    private FarmBaseMapVersion() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public bool IsBaseline { get; private set; }
    public Guid? BaselineMapVersionId { get; private set; }
    public Guid SourceSurveyOrderId { get; private set; }
    public Guid? SourceMissionGroupId { get; private set; }
    public Guid? SourceMissionId { get; private set; }
    public Guid? FarmBoundaryVersionId { get; private set; }
    public Guid? HandoffId { get; private set; }
    public double GridBearingDeg { get; private set; }
    public double RowSpacingM { get; private set; }
    public double PlantSpacingM { get; private set; }
    public FarmBaseMapStatus Status { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? ApprovedInternalBy { get; private set; }
    public DateTimeOffset? ApprovedInternalAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<ZoneMapVersion> ZoneMapVersions { get; private set; } = [];

    public static FarmBaseMapVersion CreateFirstBaseline(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        DomainGuard.Utc(createdAt);

        var mapId = Guid.NewGuid();
        return new FarmBaseMapVersion
        {
            Id = mapId,
            TenantId = context.TenantId,
            FarmId = context.FarmId,
            VersionNumber = context.VersionNumber > 0 ? context.VersionNumber : 1,
            PredecessorVersionId = null,
            IsBaseline = true,
            BaselineMapVersionId = mapId,
            SourceSurveyOrderId = context.SourceSurveyOrderId,
            SourceMissionGroupId = context.SourceMissionGroupId,
            SourceMissionId = context.SourceMissionId,
            FarmBoundaryVersionId = context.FarmBoundaryVersionId,
            HandoffId = context.HandoffId,
            GridBearingDeg = context.GridBearingDeg,
            RowSpacingM = context.RowSpacingM,
            PlantSpacingM = context.PlantSpacingM,
            Status = FarmBaseMapStatus.Draft,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public static FarmBaseMapVersion CreateFollowUp(
        FarmBaseMapPublicationContext context,
        FarmBaseMapVersion predecessor,
        FarmBaseMapVersion baseline,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(baseline);
        DomainGuard.Utc(createdAt);

        if (predecessor.FarmId != context.FarmId || predecessor.TenantId != context.TenantId)
        {
            throw new InvalidOperationException("Predecessor map must belong to the same farm and tenant.");
        }

        if (baseline.FarmId != context.FarmId || baseline.TenantId != context.TenantId || !baseline.IsBaseline)
        {
            throw new InvalidOperationException("Baseline map must be an active baseline for the same farm and tenant.");
        }

        if (context.VersionNumber <= predecessor.VersionNumber)
        {
            throw new InvalidOperationException(
                $"Follow-up version number {context.VersionNumber} must be strictly greater than predecessor version {predecessor.VersionNumber}.");
        }

        return new FarmBaseMapVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            FarmId = context.FarmId,
            VersionNumber = context.VersionNumber,
            PredecessorVersionId = predecessor.Id,
            IsBaseline = false,
            BaselineMapVersionId = baseline.Id,
            SourceSurveyOrderId = context.SourceSurveyOrderId,
            SourceMissionGroupId = context.SourceMissionGroupId,
            SourceMissionId = context.SourceMissionId,
            FarmBoundaryVersionId = context.FarmBoundaryVersionId,
            HandoffId = context.HandoffId,
            GridBearingDeg = context.GridBearingDeg,
            RowSpacingM = context.RowSpacingM,
            PlantSpacingM = context.PlantSpacingM,
            Status = FarmBaseMapStatus.Draft,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void ApproveInternal(Guid approvedBy, DateTimeOffset approvedAt)
    {
        DomainGuard.NotEmpty(approvedBy);
        DomainGuard.Utc(approvedAt);

        if (Status != FarmBaseMapStatus.Draft)
        {
            throw new InvalidOperationException($"Only draft maps can be approved internally. Current status: {Status}");
        }

        ApprovedInternalBy = approvedBy;
        ApprovedInternalAt = approvedAt;
        Status = FarmBaseMapStatus.InternalApproved;
        UpdatedAt = approvedAt;
        Version++;
    }

    public void SetPublicationProvenance(
        Guid? sourceMissionId,
        Guid? farmBoundaryVersionId,
        Guid? handoffId)
    {
        if (sourceMissionId.HasValue) DomainGuard.NotEmpty(sourceMissionId.Value);
        if (farmBoundaryVersionId.HasValue) DomainGuard.NotEmpty(farmBoundaryVersionId.Value);
        if (handoffId.HasValue) DomainGuard.NotEmpty(handoffId.Value);

        SourceMissionId = sourceMissionId ?? SourceMissionId;
        FarmBoundaryVersionId = farmBoundaryVersionId ?? FarmBoundaryVersionId;
        HandoffId = handoffId ?? HandoffId;
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(publishedBy);
        DomainGuard.Utc(publishedAt);

        if (Status is not (FarmBaseMapStatus.Draft or FarmBaseMapStatus.InternalApproved))
        {
            throw new InvalidOperationException($"Cannot publish farm base map in status '{Status}'. Must be Draft or InternalApproved.");
        }

        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        Status = FarmBaseMapStatus.Published;
        UpdatedAt = publishedAt;
        Version++;
    }

    public void Supersede(DateTimeOffset supersededAt)
    {
        DomainGuard.Utc(supersededAt);

        if (Status != FarmBaseMapStatus.Published)
        {
            throw new InvalidOperationException("Only a published farm base map can be superseded.");
        }

        Status = FarmBaseMapStatus.Superseded;
        UpdatedAt = supersededAt;
        Version++;
    }
}
