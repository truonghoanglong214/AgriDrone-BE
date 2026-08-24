namespace AgriDrone.Modules.Identity.Application.Authorization;

public static class IdentityAuthorizationPolicies
{
    public const string SystemAdmin = "Identity.SystemAdmin";

    public const string TenantMember = "Identity.TenantMember";

    public const string TenantAdmin = "Identity.TenantAdmin";

    public const string TenantOwner = "Identity.TenantOwner";

    public const string FarmMember = "Identity.FarmMember";

    public const string FarmManager = "Identity.FarmManager";
}
