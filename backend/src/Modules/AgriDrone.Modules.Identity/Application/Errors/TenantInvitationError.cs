using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class TenantInvitationError
{
    public static AppError InviteSelfNotAllowed() =>
        AppError.Validation(
            "TenantInvitation.InviteSelfNotAllowed",
            "You cannot invite yourself to the current tenant.");

    public static AppError UserAlreadyMember() =>
        AppError.Conflict(
            "TenantInvitation.UserAlreadyMember",
            "The invited user is already a member of the current tenant.");

    public static AppError AlreadyPending() =>
        AppError.Conflict(
            "TenantInvitation.AlreadyPending",
            "An active invitation already exists for this email address.");

    public static AppError InvalidOrExpired() =>
        AppError.Validation(
            "TenantInvitation.InvalidOrExpired",
            "The invitation is invalid, expired, or has already been used.");

    public static AppError RegistrationDetailsRequired() =>
        AppError.Validation(
            "TenantInvitation.RegistrationDetailsRequired",
            "Full name and a password of at least 8 characters are required for a new user.");

    public static AppError UserInactive() =>
        AppError.Conflict(
            "TenantInvitation.UserInactive",
            "The invited user account is not active.");
}
