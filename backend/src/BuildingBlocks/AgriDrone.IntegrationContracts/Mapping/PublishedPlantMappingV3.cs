namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record PublishedPlantMappingV3(
    Guid PlantId,
    Guid? SourceCandidateId,
    GeoJsonPointV3 Position,
    int RowIndex,
    int ColumnIndex,
    string LifecycleStatus,
    Guid? AppliedPlantChangeReportId);
