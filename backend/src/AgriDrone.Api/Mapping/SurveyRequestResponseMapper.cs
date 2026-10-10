using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;
using AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;
using AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;
using ExistingFarmResponse = AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest.SubmitExistingFarmSurveyRequestResponse;
using NewCustomerResponse = AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest.SubmitNewCustomerSurveyRequestResponse;
using NewFarmResponse = AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest.SubmitNewFarmSurveyRequestResponse;

namespace AgriDrone.Api.Mapping;

internal static class SurveyRequestResponseMapper
{
    public static SurveyRequestAcknowledgementApiResponse ToResponse(
        NewCustomerResponse response) =>
        ToAcknowledgement(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);

    public static SurveyRequestAcknowledgementApiResponse ToResponse(
        NewFarmResponse response) =>
        ToAcknowledgement(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);

    public static SurveyRequestAcknowledgementApiResponse ToResponse(
        ExistingFarmResponse response) =>
        ToAcknowledgement(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);

    public static PagedResult<SurveyRequestInboxItemApiResponse> ToResponse(
        PagedResult<SurveyRequestInboxItemResponse> page) =>
        new(
            page.Items.Select(ToResponse).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);

    public static SurveyRequestDetailApiResponse ToResponse(
        SurveyRequestDetailResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            MapKind(response.Kind),
            MapStatus(response.Status),
            response.TenantId,
            response.FarmId,
            response.RequestedByUserId,
            response.SurveyServiceId,
            response.SurveyServiceCode,
            response.SurveyServiceName,
            response.ApplicantName,
            response.ApplicantEmail,
            response.ApplicantPhone,
            response.FarmName,
            response.FarmAddress,
            response.ApproximateAreaHa,
            new SurveyRequestCandidateLocationApiResponse(
                response.CandidateLocation.Longitude,
                response.CandidateLocation.Latitude,
                response.CandidateLocation.MapSrid,
                response.CandidateLocation.IsApprovedFarmBoundary),
            response.EstimatedPoleCount,
            response.PreferredStartAt,
            response.PreferredEndAt,
            response.Notes,
            response.Version,
            response.CreatedAt,
            response.UpdatedAt,
            response.ChecklistVersion,
            response.ChecklistItems.Select(item =>
                    new SurveyRequestChecklistItemApiResponse(
                        item.Code,
                        item.Description))
                .ToArray(),
            response.Reviews.Select(review =>
                    new SurveyRequestReviewApiResponse(
                        review.Id,
                        MapDecision(review.Decision),
                        review.ChecklistSnapshot,
                        review.Reason,
                        review.ReviewedBy,
                        review.ReviewedAt))
                .ToArray());

    public static SurveyRequestReviewMutationApiResponse ToResponse(
        StartSurveyRequestReviewResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            MapStatus(response.Status),
            response.Version,
            response.UpdatedAt);

    public static SurveyRequestRejectionApiResponse ToResponse(
        RejectSurveyRequestResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            MapStatus(response.Status),
            response.Version,
            response.ReviewId,
            response.ReviewedAt);

    private static SurveyRequestInboxItemApiResponse ToResponse(
        SurveyRequestInboxItemResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            MapKind(response.Kind),
            MapStatus(response.Status),
            response.SurveyServiceId,
            response.SurveyServiceCode,
            response.TenantId,
            response.FarmId,
            response.ApplicantName,
            response.FarmName,
            response.ApproximateAreaHa,
            response.EstimatedPoleCount,
            response.CreatedAt,
            response.UpdatedAt,
            response.Version);

    private static SurveyRequestAcknowledgementApiResponse ToAcknowledgement(
        Guid id,
        string requestNumber,
        SurveyRequestKind kind,
        SurveyRequestStatus status,
        DateTimeOffset createdAt) =>
        new(
            id,
            requestNumber,
            MapKind(kind),
            MapStatus(status),
            createdAt);

    private static string MapKind(SurveyRequestKind kind) =>
        kind switch
        {
            SurveyRequestKind.NewCustomer => "NEW_CUSTOMER",
            SurveyRequestKind.ExistingTenantNewFarm =>
                "EXISTING_TENANT_NEW_FARM",
            SurveyRequestKind.ExistingFarmSurvey => "EXISTING_FARM_SURVEY",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported survey request kind.")
        };

    private static string MapStatus(SurveyRequestStatus status) =>
        status switch
        {
            SurveyRequestStatus.Submitted => "SUBMITTED",
            SurveyRequestStatus.UnderReview => "UNDER_REVIEW",
            SurveyRequestStatus.Approved => "APPROVED",
            SurveyRequestStatus.Rejected => "REJECTED",
            SurveyRequestStatus.Withdrawn => "WITHDRAWN",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported survey request status.")
        };

    private static string MapDecision(SurveyReviewDecision decision) =>
        decision switch
        {
            SurveyReviewDecision.Approved => "APPROVED",
            SurveyReviewDecision.Rejected => "REJECTED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision,
                "Unsupported survey review decision.")
        };
}
