namespace AgriDrone.IntegrationContracts.Plants;

public sealed record PlantReferenceV1(
    Guid PlantId,
    Guid FarmId,
    Guid ZoneId,
    string LifecycleStatus,
    Guid? MapVersionId,
    double? Latitude,
    double? Longitude,
    int? RowIndex,
    int? ColumnIndex,
    double? LocationAccuracyM);
