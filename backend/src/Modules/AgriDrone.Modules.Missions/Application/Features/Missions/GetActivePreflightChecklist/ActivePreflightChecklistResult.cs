namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;

public sealed record ActivePreflightChecklistResult(
    Guid DefinitionId,
    string Code,
    int VersionNumber,
    System.Text.Json.JsonElement Items);
