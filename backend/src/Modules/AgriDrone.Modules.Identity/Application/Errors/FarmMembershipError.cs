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

    public static AppError TargetMustBeTenantAdmin() =>
        AppError.Validation(
            "FarmMembership.TargetMustBeTenantAdmin",
            "Only an active Tenant Admin can be assigned by this operation.");

    public static AppError TargetTenantMembershipInactive() =>
        AppError.Conflict(
            "FarmMembership.TargetTenantMembershipInactive",
            "The target Tenant Admin membership must be active.");

    public static AppError ExpectedVersionRequired() =>
        AppError.Conflict(
            "FarmMembership.ExpectedVersionRequired",
            "ExpectedVersion is required when changing an existing farm assignment.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "FarmMembership.ConcurrentUpdate",
            "The farm assignment changed while the request was being processed. Reload it and try again.");
}
