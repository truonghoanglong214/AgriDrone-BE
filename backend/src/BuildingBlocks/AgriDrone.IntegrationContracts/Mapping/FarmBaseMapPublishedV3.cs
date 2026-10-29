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
    IReadOnlyList<PublishedPlantMappingV3> PlantMappings,
    IReadOnlyList<Guid> AppliedPlantChangeReportIds);
