using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetAllPreflightChecklistDefinitions;

public sealed record PreflightChecklistDefinitionResponse(
    Guid Id,
    string Code,
    int VersionNumber,
    PreflightChecklistDefinitionStatus Status,
    JsonElement Items,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? RetiredAt,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
