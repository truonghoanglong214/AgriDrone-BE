namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public enum AccessDenialReason
{
    None = 0,
    TenantMembershipNotFound = 1,
    UserInactive = 2,
    TenantInactive = 3,
    TenantMembershipInactive = 4,
    TenantRoleInsufficient = 5,
    FarmMembershipNotFound = 6,
    FarmMembershipInactive = 7,
    FarmRoleInsufficient = 8,
    ZoneAssignmentNotActive = 9,
    FarmAccessScopeUnsupported = 10
}
