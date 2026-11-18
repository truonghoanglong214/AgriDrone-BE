using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public static class MissionPlantGpsFingerprint
{
    public static string Compute(IReadOnlyList<MissionPlantGpsRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var canonical = string.Join('\n', rows
            .OrderBy(row => row.TreeId, StringComparer.Ordinal)
            .Select(row => string.Join('|',
                row.TreeId.Trim(),
                row.Row.ToString(CultureInfo.InvariantCulture),
                row.Col.ToString(CultureInfo.InvariantCulture),
                decimal.Round(row.Latitude, 12, MidpointRounding.ToEven)
                    .ToString("F12", CultureInfo.InvariantCulture),
                decimal.Round(row.Longitude, 12, MidpointRounding.ToEven)
                    .ToString("F12", CultureInfo.InvariantCulture),
                decimal.Round(row.Confidence, 15, MidpointRounding.ToEven)
                    .ToString("F15", CultureInfo.InvariantCulture),
                row.Status.Trim(),
                row.Source.Trim())));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
