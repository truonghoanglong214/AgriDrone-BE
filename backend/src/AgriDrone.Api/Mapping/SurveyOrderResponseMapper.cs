using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;
using AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Mapping;

internal static class SurveyOrderResponseMapper
{
    public static GetSurveyOrderBoundaryScopeApiResponse ToResponse(
        GetSurveyOrderBoundaryScopeResponse response) =>
        new(
            response.SurveyOrderId,
            response.OrderNumber,
            response.TenantId,
            response.FarmId,
            MapStatus(response.OrderStatus),
            response.OrderVersion,
            response.RequiresBaselineMapping,
            response.CurrentApprovedBoundary is null
                ? null
                : new SurveyOrderBoundaryApiResponse(
                    response.CurrentApprovedBoundary.FarmBoundaryId,
                    response.CurrentApprovedBoundary.VersionNumber,
                    response.CurrentApprovedBoundary.Version,
                    GeoJsonGeometryMapper.FromPolygon(
                        response.CurrentApprovedBoundary.Geometry)!,
                    response.CurrentApprovedBoundary.Source
                        .ToUpperInvariant(),
                    response.CurrentApprovedBoundary.EvidenceReference,
                    response.CurrentApprovedBoundary.ReviewedBy,
                    response.CurrentApprovedBoundary.ReviewedAt),
            response.Zones.Select(zone =>
                    new SurveyOrderZoneScopeApiResponse(
                        zone.ZoneId,
                        zone.Code,
                        zone.Name,
                        GeoJsonGeometryMapper.FromPolygon(zone.Boundary),
                        zone.Status.ToUpperInvariant(),
                        zone.Version))
                .ToArray());

    public static VerifySurveyOrderBoundaryAndScopeApiResponse ToResponse(
        VerifySurveyOrderBoundaryAndScopeResponse response) =>
        new(
            response.SurveyOrderId,
            response.OrderNumber,
            response.TenantId,
            response.FarmId,
            response.FarmBoundaryId,
            response.FarmBoundaryVersionNumber,
            response.SystemManagerProfileId,
            response.RequiresBaselineMapping,
            response.CurrentFarmBaseMapVersionId,
            response.ConfirmedSurveyPoleCount,
            MapStatus(response.OrderStatus),
            response.OrderVersion,
            response.VerifiedAt);

    private static string MapStatus(SurveyOrderStatus status) =>
        status switch
        {
            SurveyOrderStatus.PendingBoundaryVerification =>
                "PENDING_BOUNDARY_VERIFICATION",
            SurveyOrderStatus.AwaitingBaselineAppointment =>
                "AWAITING_BASELINE_APPOINTMENT",
            SurveyOrderStatus.BaselineReady => "BASELINE_READY",
            SurveyOrderStatus.BaselineInProgress => "BASELINE_IN_PROGRESS",
            SurveyOrderStatus.AwaitingBaselineReview =>
                "AWAITING_BASELINE_REVIEW",
            SurveyOrderStatus.AwaitingPricing => "AWAITING_PRICING",
            SurveyOrderStatus.AwaitingPaidAppointment =>
                "AWAITING_PAID_APPOINTMENT",
            SurveyOrderStatus.AwaitingPayment => "AWAITING_PAYMENT",
            SurveyOrderStatus.ReadyForPaidService =>
                "READY_FOR_PAID_SERVICE",
            SurveyOrderStatus.InProgress => "IN_PROGRESS",
            SurveyOrderStatus.PendingReview => "PENDING_REVIEW",
            SurveyOrderStatus.Completed => "COMPLETED",
            SurveyOrderStatus.Cancelled => "CANCELLED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported SurveyOrder status.")
        };
}
