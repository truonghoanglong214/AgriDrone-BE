namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

public sealed record ManagePreflightChecklistDefinitionResult(
    Guid DefinitionId,
    string Code,
    int VersionNumber,
    DateTimeOffset EffectiveFrom);
