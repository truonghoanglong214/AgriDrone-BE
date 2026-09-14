using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class FarmMembershipError
{
    public static AppError NotFound() =>
        AppError.NotFound(
            "FarmMembership.NotFound",
            "The selected user does not have an assignment in this farm.");

    public static AppError FarmNotFound() =>
        AppError.NotFound(
            "FarmMembership.FarmNotFound",
            "The selected farm was not found in the current tenant.");

    public static AppError TargetTenantRoleNotAssignable() =>
        AppError.Validation(
            "FarmMembership.TargetTenantRoleNotAssignable",
            "Only an active Member or Tenant Admin can receive a farm assignment.");

    public static AppError TenantAdminMustBeManager() =>
        AppError.Validation(
            "FarmMembership.TenantAdminMustBeManager",
            "A Tenant Admin can only receive the MANAGER farm role.");

    public static AppError TargetTenantMembershipInactive() =>
        AppError.Conflict(
            "FarmMembership.TargetTenantMembershipInactive",
            "The target tenant membership must be active.");

    public static AppError InvalidZones() =>
        AppError.Validation(
            "FarmMembership.InvalidZones",
            "Every selected zone must be active and belong to the selected farm.");

    public static AppError ExpectedVersionRequired() =>
        AppError.Conflict(
            "FarmMembership.ExpectedVersionRequired",
            "ExpectedVersion is required when changing an existing farm assignment.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "FarmMembership.ConcurrentUpdate",
            "The farm assignment changed while the request was being processed. Reload it and try again.");
}
