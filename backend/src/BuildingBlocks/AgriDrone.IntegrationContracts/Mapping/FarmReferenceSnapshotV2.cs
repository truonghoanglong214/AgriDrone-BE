namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record FarmReferenceSnapshotV2(
    Guid SurveyOrderId,
    Guid FarmId,
    Guid TenantId,
    Guid? CurrentFarmBaseMapVersionId,
    int? CurrentVersionNumber,
    IReadOnlyList<ZoneReferenceV2> Zones,
    IReadOnlyList<PlantReferenceSnapshotV2> Plants,
    DateTimeOffset CapturedAt);

public sealed record ZoneReferenceV2(
    Guid ZoneId,
    Guid? CurrentZoneMapVersionId,
    bool IsActive);

public sealed record PlantReferenceSnapshotV2(
    Guid PlantId,
    Guid ZoneId,
    string LifecycleStatus,
    double Latitude,
    double Longitude,
    Guid CurrentMapVersionId);

public interface IFarmReferenceSnapshotQuery
{
    Task<FarmReferenceSnapshotV2?> GetCurrentAsync(
        Guid surveyOrderId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}
