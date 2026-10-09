using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record ActivePreflightChecklistResponse(
    Guid DefinitionId,
    string Code,
    int VersionNumber,
    JsonElement Items);
