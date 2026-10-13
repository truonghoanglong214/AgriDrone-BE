namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record FarmBaseMapPublishedV2(
    Guid CausationId,
    Guid PublicationId,
    Guid SurveyOrderId,
    Guid SourceMissionId,
    Guid FarmId,
    Guid FarmBaseMapVersionId,
    int VersionNumber,
    DateTimeOffset PublishedAt,
    IReadOnlyList<PublishedZoneMapV2> Zones);

public sealed record PublishedZoneMapV2(
    Guid ZoneId,
    Guid ZoneMapVersionId,
    int VersionNumber,
    IReadOnlyList<PlantMappingV1> PlantMappings);
