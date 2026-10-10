using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyApprovalError
{
    public const string RequestNotUnderReviewCode =
        "SurveyApproval.RequestNotUnderReview";
    public const string RequestContextInvalidCode =
        "SurveyApproval.RequestContextInvalid";
    public const string ServiceUnavailableCode =
        "SurveyApproval.ServiceUnavailable";
    public const string TenantOwnerNotEligibleCode =
        "SurveyApproval.TenantOwnerNotEligible";
    public const string FarmUnavailableCode =
        "SurveyApproval.FarmUnavailable";
    public const string SelectedManagerRequiredCode =
        "SurveyApproval.SelectedManagerRequired";
    public const string ManagerNotAssignableCode =
        "SurveyApproval.ManagerNotAssignable";
    public const string PrimaryManagerRequiredCode =
        "SurveyApproval.PrimaryManagerRequired";
    public const string ExistingFarmManagerMismatchCode =
        "SurveyApproval.ExistingFarmManagerMismatch";
    public const string ConcurrentApprovalCode =
        "SurveyApproval.ConcurrentApproval";
    public const string TenantProvisioningConflictCode =
        "SurveyApproval.TenantProvisioningConflict";
    public const string FarmProvisioningConflictCode =
        "SurveyApproval.FarmProvisioningConflict";
    public const string PrimaryAssignmentConflictCode =
        "SurveyApproval.PrimaryAssignmentConflict";
    public const string OwnerInvitationConflictCode =
        "SurveyApproval.OwnerInvitationConflict";
    public const string OrderNumberConflictCode =
        "SurveyApproval.OrderNumberConflict";

    public static AppError RequestNotUnderReview(Guid requestId) =>
        AppError.Conflict(
            RequestNotUnderReviewCode,
            $"Survey request '{requestId}' is not under review.");

    public static AppError RequestContextInvalid() =>
        AppError.Failure(
            RequestContextInvalidCode,
            "The persisted survey request context is inconsistent with its request kind.");

    public static AppError ServiceUnavailable() =>
        AppError.Validation(
            ServiceUnavailableCode,
            "The survey service is no longer eligible for approval.");

    public static AppError TenantOwnerNotEligible() =>
        AppError.Validation(
            TenantOwnerNotEligibleCode,
            "The requesting TenantOwner or Tenant is no longer active.");

    public static AppError FarmUnavailable() =>
        AppError.Validation(
            FarmUnavailableCode,
            "The referenced Farm is inactive, missing, or does not belong to the request Tenant.");

    public static AppError SelectedManagerRequired() =>
        AppError.Validation(
            SelectedManagerRequiredCode,
            "A SystemManager must be selected when approving a request for a new Farm.");

    public static AppError ManagerNotAssignable() =>
        AppError.Validation(
            ManagerNotAssignableCode,
            "The selected SystemManager is not active, available, and flight-qualified at approval time.");

    public static AppError PrimaryManagerRequired() =>
        AppError.Validation(
            PrimaryManagerRequiredCode,
            "The existing Farm does not have a valid active primary SystemManager assignment.");

    public static AppError ExistingFarmManagerMismatch() =>
        AppError.Conflict(
            ExistingFarmManagerMismatchCode,
            "The supplied SystemManager does not match the Farm's current primary assignment. Reassignment requires the explicit audited reassignment flow.");

    public static AppError ConcurrentApproval() =>
        AppError.Conflict(
            ConcurrentApprovalCode,
            "The survey request was approved or changed by another operation. Reload the existing approval result.");

    public static AppError TenantProvisioningConflict() =>
        AppError.Conflict(
            TenantProvisioningConflictCode,
            "A unique Tenant code could not be allocated. Retry the approval operation.");

    public static AppError FarmProvisioningConflict() =>
        AppError.Conflict(
            FarmProvisioningConflictCode,
            "A unique Farm code could not be allocated. Retry the approval operation.");

    public static AppError PrimaryAssignmentConflict() =>
        AppError.Conflict(
            PrimaryAssignmentConflictCode,
            "The Farm already has a different active primary SystemManager assignment.");

    public static AppError OwnerInvitationConflict() =>
        AppError.Conflict(
            OwnerInvitationConflictCode,
            "The Tenant already has an active Owner or pending Owner provisioning invitation.");

    public static AppError OrderNumberConflict() =>
        AppError.Conflict(
            OrderNumberConflictCode,
            "A unique SurveyOrder number could not be allocated. Retry the approval operation.");
}
