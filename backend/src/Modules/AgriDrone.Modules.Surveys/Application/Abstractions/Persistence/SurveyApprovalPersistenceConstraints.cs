namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

public static class SurveyApprovalPersistenceConstraints
{
    public const string RequestOrder = "uq_survey_orders_request";

    public const string OrderNumber = "uq_survey_orders_number";

    public const string ActiveFarmAssignment =
        "uq_farm_manager_assignments_active_farm";

    public const string ActiveTenantCode = "ux_tenants_code_active";

    public const string ActiveFarmCode = "ux_farms_tenant_code_active";

    public const string PendingOwnerInvitation =
        "uq_tenant_invitations_pending_owner_provisioning";
}
