using System.Security.Cryptography;
using System.Text;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;

namespace AgriDrone.Modules.Identity.Infrastructure.Security;

internal sealed class PasswordResetTokenService : IPasswordResetTokenService
{
    public PasswordResetTokenResult Generate()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var plainTextToken = Convert.ToHexString(tokenBytes);

        return new PasswordResetTokenResult(
            plainTextToken,
            Hash(plainTextToken));
    }

    public string Hash(string plainTextToken)
    {
        var hashBytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(plainTextToken));

        return Convert.ToHexString(hashBytes);
    }
}
