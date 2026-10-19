namespace AgriDrone.Api.Contracts.PlantConditions;

public sealed record CreateSourcedGuidanceRequest(
    string Code,
    Guid PlantConditionId,
    string SourceTitle,
    string SourceReference,
    string GuidanceText,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record ChangeSourcedGuidanceStatusRequest(uint ExpectedVersion);
