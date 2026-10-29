using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed record FarmBaseMapPublicationContext
{
    public FarmBaseMapPublicationContext(
        Guid sourceSurveyOrderId,
        Guid tenantId,
        Guid farmId,
        int versionNumber,
        Guid? sourceMissionGroupId = null,
        Guid? sourceMissionId = null,
        Guid? farmBoundaryVersionId = null,
        Guid? handoffId = null,
        double gridBearingDeg = 0,
        double rowSpacingM = 3,
        double plantSpacingM = 2.5)
    {
        DomainGuard.NotEmpty(sourceSurveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        if (sourceMissionGroupId.HasValue) DomainGuard.NotEmpty(sourceMissionGroupId.Value);
        if (sourceMissionId.HasValue) DomainGuard.NotEmpty(sourceMissionId.Value);
        if (farmBoundaryVersionId.HasValue) DomainGuard.NotEmpty(farmBoundaryVersionId.Value);
        if (handoffId.HasValue) DomainGuard.NotEmpty(handoffId.Value);
        ArgumentOutOfRangeException.ThrowIfNegative(gridBearingDeg);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(gridBearingDeg, 360);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowSpacingM);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plantSpacingM);

        SourceSurveyOrderId = sourceSurveyOrderId;
        TenantId = tenantId;
        FarmId = farmId;
        VersionNumber = versionNumber;
        SourceMissionGroupId = sourceMissionGroupId;
        SourceMissionId = sourceMissionId;
        FarmBoundaryVersionId = farmBoundaryVersionId;
        HandoffId = handoffId;
        GridBearingDeg = gridBearingDeg;
        RowSpacingM = rowSpacingM;
        PlantSpacingM = plantSpacingM;
    }

    public Guid SourceSurveyOrderId { get; }
    public Guid TenantId { get; }
    public Guid FarmId { get; }
    public int VersionNumber { get; }
    public Guid? SourceMissionGroupId { get; }
    public Guid? SourceMissionId { get; }
    public Guid? FarmBoundaryVersionId { get; }
    public Guid? HandoffId { get; }
    public double GridBearingDeg { get; }
    public double RowSpacingM { get; }
    public double PlantSpacingM { get; }
}
