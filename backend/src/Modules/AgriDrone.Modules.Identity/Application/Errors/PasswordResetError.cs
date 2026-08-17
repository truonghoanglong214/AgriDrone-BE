using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class PasswordResetError
{
    public static AppError InvalidOrExpiredToken() =>
        AppError.Validation(
            "PasswordReset.InvalidOrExpiredToken",
            "The password reset token is invalid, expired, or has already been used.");
}
