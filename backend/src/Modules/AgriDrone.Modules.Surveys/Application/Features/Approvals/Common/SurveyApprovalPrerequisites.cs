using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;

internal sealed record SurveyApprovalPrerequisites(
    SurveyApprovalServiceReference Service,
    SurveyApprovalManagerReference Manager,
    SurveyApprovalTenantOwnerReference? TenantOwner,
    SurveyApprovalFarmReference? Farm,
    SurveyApprovalPrimaryAssignmentReference? CurrentPrimaryAssignment,
    SurveyApprovalFarmBaselineReference? FarmBaseline,
    bool RequiresBaselineMapping,
    SurveyApprovalPreviousOrderReference? PreviousCompatibleOrder);

internal static class SurveyApprovalManagerEligibilityPolicy
{
    public static bool IsAssignable(
        SurveyApprovalManagerReference manager,
        DateTimeOffset now) =>
        manager.IsUserActive &&
        manager.IsProfileActive &&
        manager.IsAvailable &&
        manager.IsFlightQualified &&
        manager.QualificationExpiresAt is DateTimeOffset expiresAt &&
        expiresAt > now;
}

internal static class SurveyApprovalTenantOwnerEligibilityPolicy
{
    public static bool IsEligible(
        SurveyApprovalTenantOwnerReference owner) =>
        owner.IsOwner &&
        owner.IsMembershipActive &&
        owner.IsTenantActive &&
        owner.IsUserActive;
}

internal static class SurveyApprovalBaselineRequirementResolver
{
    public static bool RequiresBaselineMapping(
        SurveyApprovalFarmBaselineReference baseline) =>
        !baseline.CurrentPublishedBaseMapVersionId.HasValue ||
        baseline.ConfirmedActivePoleCount is null or <= 0;
}
