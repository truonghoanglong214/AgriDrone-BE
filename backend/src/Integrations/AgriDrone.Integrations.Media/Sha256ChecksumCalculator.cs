using System.Security.Cryptography;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;

namespace AgriDrone.Integrations.Media;

internal sealed class Sha256ChecksumCalculator
    : IChecksumCalculator
{
    public async Task<string> CalculateAsync(
        Stream content,
        string algorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!string.Equals(
                algorithm,
                "SHA256",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Checksum algorithm '{algorithm}' is not supported.");
        }

        using var sha256 = SHA256.Create();

        var hash = await sha256.ComputeHashAsync(
            content,
            cancellationToken);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }
}