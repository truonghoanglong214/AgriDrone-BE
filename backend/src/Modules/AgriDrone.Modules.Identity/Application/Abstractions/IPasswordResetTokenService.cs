using AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;

namespace AgriDrone.Modules.Identity.Application.Abstractions;

public interface IPasswordResetTokenService
{
    PasswordResetTokenResult Generate();

    string Hash(string plainTextToken);
}
