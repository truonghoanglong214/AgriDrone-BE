using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public static class DroneCapabilityPolicy
{
    public static string RequiredFor(MissionPurpose purpose) => purpose switch
    {
        MissionPurpose.BaselineMapping => "BASELINE_MAPPING",
        MissionPurpose.PlantHealth => "PLANT_HEALTH",
        MissionPurpose.HarvestReadiness => "HARVEST_READINESS",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    public static bool Supports(JsonElement specifications, MissionPurpose purpose)
    {
        if (specifications.ValueKind != JsonValueKind.Object ||
            !specifications.TryGetProperty("capabilities", out var capabilities) ||
            capabilities.ValueKind != JsonValueKind.Array)
            return false;

        var required = RequiredFor(purpose);
        return capabilities.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String &&
            // Accept legacy lowercase values already persisted before the API
            // contract moved to UPPER_SNAKE_CASE.
            string.Equals(item.GetString(), required, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValid(JsonElement specifications)
    {
        if (specifications.ValueKind != JsonValueKind.Object)
            return false;
        if (!specifications.TryGetProperty("capabilities", out var capabilities))
            return true;
        if (capabilities.ValueKind != JsonValueKind.Array)
            return false;
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in capabilities.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                item.GetString() is not string value ||
                value is not ("BASELINE_MAPPING" or "PLANT_HEALTH" or "HARVEST_READINESS") ||
                !values.Add(value))
                return false;
        }
        return true;
    }
}
