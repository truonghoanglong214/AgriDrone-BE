using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class TenantMembershipError
{
    public static AppError NotFound() =>
        AppError.NotFound(
            "TenantMembership.NotFound",
            "Tenant membership was not found.");

    public static AppError UserNotInAnyTenant(string email) =>
        AppError.Forbidden(
            "User.NotInAnyTenant",
            $"User with email '{email}' is not a member of any tenant.");

    public static AppError SelfRoleChangeForbidden() =>
        AppError.Forbidden(
            "TenantMembership.SelfRoleChangeForbidden",
            "You cannot change your own tenant role.");

    public static AppError OwnerRoleProtected() =>
        AppError.Conflict(
            "TenantMembership.OwnerRoleProtected",
            "OWNER cannot be assigned or removed through this operation.");

    public static AppError OwnerMembershipProtected() =>
        AppError.Conflict(
            "TenantMembership.OwnerMembershipProtected",
            "The active OWNER membership cannot be deactivated.");

    public static AppError Inactive() =>
        AppError.Conflict(
            "TenantMembership.Inactive",
            "An inactive tenant membership cannot change role.");

    public static AppError TargetUserInactive() =>
        AppError.Conflict(
            "TenantMembership.UserInactive",
            "The target user is inactive.");
}
