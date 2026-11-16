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

    public static FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        DomainGuard.Utc(createdAt);

        return new FarmBaseMapVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            FarmId = context.FarmId,
            VersionNumber = context.VersionNumber,
            PredecessorVersionId = context.PredecessorVersionId,
            IsBaseline = context.IsBaseline,
            BaselineMapVersionId = context.BaselineMapVersionId,
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

        ApprovedInternalBy = approvedBy;
        ApprovedInternalAt = approvedAt;
        UpdatedAt = approvedAt;
        Version++;
    }

    public void SetLineage(
        bool isBaseline,
        Guid? predecessorVersionId,
        Guid? baselineMapVersionId)
    {
        if (isBaseline && predecessorVersionId.HasValue)
        {
            throw new InvalidOperationException("A baseline farm base map cannot have a predecessor version.");
        }

        if (predecessorVersionId.HasValue) DomainGuard.NotEmpty(predecessorVersionId.Value);
        if (baselineMapVersionId.HasValue) DomainGuard.NotEmpty(baselineMapVersionId.Value);

        IsBaseline = isBaseline;
        PredecessorVersionId = predecessorVersionId;
        BaselineMapVersionId = baselineMapVersionId;
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

        if (Status != FarmBaseMapStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft farm base map can be published.");
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
