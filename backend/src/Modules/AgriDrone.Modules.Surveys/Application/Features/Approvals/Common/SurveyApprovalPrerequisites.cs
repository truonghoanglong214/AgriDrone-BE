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
    SurveyApprovalOrderCompatibilityDecision OrderCompatibility);

internal sealed record SurveyApprovalOrderCompatibilityDecision(
    Guid? PreviousCompatibleOrderId,
    string PolicyVersion,
    string Reason);

internal static class SurveyApprovalOrderCompatibilityPolicy
{
    public const string Version = "plant-health-order-compatibility.v1";
    public const string LabelDatasetVersion = "AGRIDRONE_PILOT_DATASET_V1";
    public const string AnnotationRuleVersion =
        "AGRIDRONE_ANNOTATION_RULES_V1";

    public const string Compatible = "COMPATIBLE";
    public const string NewFarm = "NOT_APPLICABLE_NEW_FARM";
    public const string NoPreviousOrder = "NO_PREVIOUS_PUBLISHED_ORDER";
    public const string CurrentBaseMapUnavailable =
        "CURRENT_BASE_MAP_UNAVAILABLE";
    public const string CurrentPlantInventoryUnavailable =
        "CURRENT_PLANT_INVENTORY_UNAVAILABLE";
    public const string PreviousBaseMapUnavailable =
        "PREVIOUS_BASE_MAP_UNAVAILABLE";
    public const string PlantIdentityMismatch = "PLANT_IDENTITY_MISMATCH";
    public const string ClassSemanticsUnavailable =
        "CLASS_SEMANTICS_UNAVAILABLE";
    public const string ClassSemanticsMismatch = "CLASS_SEMANTICS_MISMATCH";
    public const string AnnotationSemanticsUnavailable =
        "ANNOTATION_SEMANTICS_UNAVAILABLE";
    public const string AnnotationSemanticsMismatch =
        "ANNOTATION_SEMANTICS_MISMATCH";

    public static SurveyApprovalOrderCompatibilityDecision ForNewFarm() =>
        Incompatible(NewFarm);

    public static SurveyApprovalOrderCompatibilityDecision Evaluate(
        SurveyApprovalFarmBaselineReference baseline,
        SurveyApprovalPreviousOrderReference? candidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        if (candidate is null)
        {
            return Incompatible(NoPreviousOrder);
        }

        if (baseline.CurrentPublishedBaseMapVersionId is not Guid currentMapId)
        {
            return Incompatible(CurrentBaseMapUnavailable);
        }

        if (baseline.ConfirmedActivePoleCount is null or <= 0)
        {
            return Incompatible(CurrentPlantInventoryUnavailable);
        }

        if (candidate.FarmBaseMapVersionId is not Guid previousMapId)
        {
            return Incompatible(PreviousBaseMapUnavailable);
        }

        if (previousMapId != currentMapId)
        {
            return Incompatible(PlantIdentityMismatch);
        }

        if (string.IsNullOrWhiteSpace(candidate.LabelDatasetVersion))
        {
            return Incompatible(ClassSemanticsUnavailable);
        }

        if (!string.Equals(
                candidate.LabelDatasetVersion,
                LabelDatasetVersion,
                StringComparison.Ordinal))
        {
            return Incompatible(ClassSemanticsMismatch);
        }

        if (string.IsNullOrWhiteSpace(candidate.AnnotationRuleVersion))
        {
            return Incompatible(AnnotationSemanticsUnavailable);
        }

        if (!string.Equals(
                candidate.AnnotationRuleVersion,
                AnnotationRuleVersion,
                StringComparison.Ordinal))
        {
            return Incompatible(AnnotationSemanticsMismatch);
        }

        return new SurveyApprovalOrderCompatibilityDecision(
            candidate.SurveyOrderId,
            Version,
            Compatible);
    }

    private static SurveyApprovalOrderCompatibilityDecision Incompatible(
        string reason) => new(null, Version, reason);
}

internal static class SurveyApprovalManagerEligibilityPolicy
{
    public static bool IsAssignable(
        SurveyApprovalManagerReference manager,
        DateTimeOffset now) =>
        manager.IsUserActive &&
        manager.IsProfileActive &&
        manager.IsAvailable;
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
