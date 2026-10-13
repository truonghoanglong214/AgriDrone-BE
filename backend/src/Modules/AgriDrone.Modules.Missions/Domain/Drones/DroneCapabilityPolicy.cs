using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Domain.Drones;

public static class DroneCapabilityPolicy
{
    public static string RequiredFor(MissionPurpose purpose) => purpose switch
    {
        MissionPurpose.BaselineMapping => "baseline_mapping",
        MissionPurpose.PlantHealth => "plant_health",
        MissionPurpose.HarvestReadiness => "harvest_readiness",
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
            string.Equals(item.GetString(), required, StringComparison.Ordinal));
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
                value is not ("baseline_mapping" or "plant_health" or "harvest_readiness") ||
                !values.Add(value))
                return false;
        }
        return true;
    }
}
