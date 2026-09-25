using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class SystemManagerInvitationError
{
    public static AppError AlreadyPending() =>
        AppError.Conflict(
            "SystemManagerInvitation.AlreadyPending",
            "An active System Manager invitation already exists for this email.");

    public static AppError InvalidOrExpired() =>
        AppError.Validation(
            "SystemManagerInvitation.InvalidOrExpired",
            "The invitation is invalid, expired, or has already been used.");

    public static AppError RegistrationDetailsRequired() =>
        AppError.Validation(
            "SystemManagerInvitation.RegistrationDetailsRequired",
            "Full name and a password of at least 8 characters are required.");

    public static AppError AlreadySystemManager() =>
        AppError.Conflict(
            "SystemManagerInvitation.AlreadySystemManager",
            "The user is already registered as a System Manager.");

    public static AppError UserInactive() =>
        AppError.Conflict(
            "SystemManagerInvitation.UserInactive",
            "The invited user account is inactive.");
}