using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record ManagePreflightChecklistConditionsRequest(
    JsonElement ItemsToAdd,
    IReadOnlyList<string> KeysToRemove);
