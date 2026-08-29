using AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Services;

public interface IPasswordResetTokenService
{
    PasswordResetTokenResult Generate();

    string Hash(string plainTextToken);
}
