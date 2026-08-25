using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class PasswordError
{
    public static AppError Incorrect() =>
        AppError.Forbidden(
            "Password.NotCorrect",
            "Old password is not correct.");
}
