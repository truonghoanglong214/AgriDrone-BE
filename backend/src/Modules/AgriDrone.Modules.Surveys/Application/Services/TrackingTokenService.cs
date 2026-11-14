using System.Security.Cryptography;
using System.Text;

namespace AgriDrone.Modules.Surveys.Application.Services;

internal static class TrackingTokenService
{
    public static string GenerateToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return $"trk_{Convert.ToHexStringLower(randomBytes)}";
    }

    public static string ComputeHash(string plaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        var bytes = Encoding.UTF8.GetBytes(plaintextToken.Trim());
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
