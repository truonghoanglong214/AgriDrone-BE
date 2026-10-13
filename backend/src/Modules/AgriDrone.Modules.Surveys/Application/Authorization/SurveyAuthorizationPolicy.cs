namespace AgriDrone.Modules.Surveys.Application.Authorization;

public enum SurveyActorKind
{
    Public,
    SystemAdmin,
    SystemManager,
    TenantOwner
}

public enum SurveyAuthorizationAction
{
    ViewPublicCatalogue,
    SubmitNewCustomerRequest,
    SubmitTenantRequest,
    ReviewRequest,
    ManageManagerAssignments,
    ConfirmScope,
    ProposeAppointment,
    ConfirmOrRescheduleAppointment,
    InitiatePayment,
    ReconcilePayment,
    ReviewOrPublishResult,
    ViewPendingResult,
    ViewPublishedResult
}

public sealed record SurveyAuthorizationContext(
    SurveyActorKind Actor,
    bool OwnsTenant = false,
    bool HasActiveFarmAssignment = false,
    bool HasCurrentQualification = false);

public static class SurveyAuthorizationPolicy
{
    public static bool IsAllowed(
        SurveyAuthorizationAction action,
        SurveyAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (action == SurveyAuthorizationAction.ViewPublicCatalogue)
        {
            return true;
        }

        return context.Actor switch
        {
            SurveyActorKind.Public =>
                action == SurveyAuthorizationAction.SubmitNewCustomerRequest,

            SurveyActorKind.SystemAdmin => action is
                SurveyAuthorizationAction.ReviewRequest or
                SurveyAuthorizationAction.ManageManagerAssignments or
                SurveyAuthorizationAction.ReconcilePayment or
                SurveyAuthorizationAction.ViewPendingResult or
                SurveyAuthorizationAction.ViewPublishedResult,

            SurveyActorKind.SystemManager =>
                context.HasActiveFarmAssignment &&
                context.HasCurrentQualification &&
                action is
                    SurveyAuthorizationAction.ConfirmScope or
                    SurveyAuthorizationAction.ProposeAppointment or
                    SurveyAuthorizationAction.ReviewOrPublishResult or
                    SurveyAuthorizationAction.ViewPendingResult or
                    SurveyAuthorizationAction.ViewPublishedResult,

            SurveyActorKind.TenantOwner =>
                context.OwnsTenant &&
                action is
                    SurveyAuthorizationAction.SubmitTenantRequest or
                    SurveyAuthorizationAction.ConfirmOrRescheduleAppointment or
                    SurveyAuthorizationAction.InitiatePayment or
                    SurveyAuthorizationAction.ViewPublishedResult,

            _ => false
        };
    }
}
