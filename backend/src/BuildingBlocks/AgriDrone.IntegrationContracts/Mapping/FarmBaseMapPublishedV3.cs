namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record FarmBaseMapPublishedV3(
    Guid CausationId,
    Guid PublicationId,
    Guid SurveyOrderId,
    Guid SourceMissionId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid FarmBaseMapVersionId,
    int VersionNumber,
    int ConfirmedSurveyPoleCount,
    Guid ConfirmedBy,
    DateTimeOffset PublishedAt,
    IReadOnlyList<PublishedZoneMapV3> Zones,
    IReadOnlyList<Guid> AppliedPlantChangeReportIds);

public sealed record PublishedZoneMapV3(
    Guid ZoneId,
    Guid ZoneMapVersionId,
    int VersionNumber,
    IReadOnlyList<PublishedPlantMappingV3> PlantMappings);

public sealed record PublishedPlantMappingV3(
    Guid PlantId,
    Guid? SourceCandidateId,
    GeoJsonPointV3 Position,
    int RowIndex,
    int ColumnIndex,
    string LifecycleStatus,
    Guid? AppliedPlantChangeReportId);

public static class PublishedPlantLifecycleStatusesV3
{
    public const string Active = "ACTIVE";
}
