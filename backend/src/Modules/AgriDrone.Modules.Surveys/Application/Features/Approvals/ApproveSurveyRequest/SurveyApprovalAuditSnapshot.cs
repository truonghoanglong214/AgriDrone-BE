using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal static class SurveyApprovalAuditSnapshot
{
    public static JsonDocument CreateBefore(SurveyRequest request) =>
        JsonSerializer.SerializeToDocument(new
        {
            request.RequestNumber,
            request.Kind,
            RequestStatus = request.Status,
            request.TenantId,
            request.FarmId,
            request.SurveyServiceId,
            request.Version
        });

    public static JsonDocument CreateAfter(
        SurveyRequest request,
        SurveyRequestReview review,
        SurveyApprovalOrchestrationResult approval,
        string checklistVersion) =>
        JsonSerializer.SerializeToDocument(new
        {
            request.RequestNumber,
            request.Kind,
            request.Status,
            TenantId = approval.TenantId,
            FarmId = approval.FarmId,
            request.SurveyServiceId,
            request.Version,
            ApprovalReviewId = review.Id,
            SurveyOrderId = approval.Order.Id,
            approval.Order.OrderNumber,
            OrderStatus = approval.Order.Status,
            approval.SystemManagerProfileId,
            approval.PrimaryManagerAssignmentId,
            approval.OwnerInvitationId,
            approval.Order.RequiresBaselineMapping,
            approval.Order.PreviousCompatibleOrderId,
            approval.Order.PreviousOrderCompatibilityPolicyVersion,
            approval.Order.PreviousOrderCompatibilityReason,
            ChecklistVersion = checklistVersion
        });
}
