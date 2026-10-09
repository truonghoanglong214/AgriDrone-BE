using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record ManagePreflightChecklistDefinitionRequest(JsonElement Items);
