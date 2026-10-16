using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Requests;

namespace AgriDrone.Api.Contracts.Surveys.Approvals;

public sealed record ApproveSurveyRequest(
    Guid? SelectedSystemManagerProfileId,
    [param: Required]
    [param: StringLength(4_000, MinimumLength = 1)]
    string Reason,
    [param: Required]
    SurveyRequestReviewChecklistRequest Checklist,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedVersion);

public sealed record ApproveSurveyRequestApiResponse(
    Guid SurveyRequestId,
    string RequestNumber,
    string RequestStatus,
    uint RequestVersion,
    Guid ApprovalReviewId,
    Guid SurveyOrderId,
    string OrderNumber,
    string OrderStatus,
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    Guid? PrimaryManagerAssignmentId,
    Guid? OwnerInvitationId,
    bool RequiresBaselineMapping,
    Guid? PreviousCompatibleOrderId,
    DateTimeOffset ApprovedAt);
