namespace AgriDrone.SharedInfrastructure.Authorization;

public static class AccessAuthorizationPolicies
{
    public const string SystemAdmin = "Access.SystemAdmin";

    public const string TenantMember = "Access.TenantMember";

    public const string TenantAdmin = "Access.TenantAdmin";

    public const string TenantOwner = "Access.TenantOwner";

    public const string FarmRead = "Access.FarmRead";

    public const string FarmManage = "Access.FarmManage";

    public const string ZoneRead = "Access.ZoneRead";

    public const string ZoneManage = "Access.ZoneManage";
}
