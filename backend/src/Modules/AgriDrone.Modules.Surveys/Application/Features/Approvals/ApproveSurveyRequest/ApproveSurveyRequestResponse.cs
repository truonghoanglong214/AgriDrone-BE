using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

public sealed record ApproveSurveyRequestResponse(
    Guid SurveyRequestId,
    string RequestNumber,
    SurveyRequestStatus RequestStatus,
    uint RequestVersion,
    Guid ApprovalReviewId,
    Guid SurveyOrderId,
    string OrderNumber,
    SurveyOrderStatus OrderStatus,
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    Guid? PrimaryManagerAssignmentId,
    Guid? OwnerInvitationId,
    bool RequiresBaselineMapping,
    Guid? PreviousCompatibleOrderId,
    DateTimeOffset ApprovedAt);
