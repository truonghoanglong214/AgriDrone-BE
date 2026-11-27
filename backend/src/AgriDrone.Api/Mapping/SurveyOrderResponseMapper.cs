using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetAssignedSurveyOrders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSystemAdminSurveyOrders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;
using AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Api.Mapping;

internal static class SurveyOrderResponseMapper
{
    public static PagedResult<SystemAdminSurveyOrderApiResponse> ToResponse(
        PagedResult<SystemAdminSurveyOrderResponse> page) =>
        new(
            page.Items.Select(response => new SystemAdminSurveyOrderApiResponse(
                    response.SurveyOrderId,
                    response.OrderNumber,
                    response.TenantId,
                    response.FarmId,
                    response.FarmCode,
                    response.FarmName,
                    response.SurveyServiceId,
                    response.SurveyServiceCode,
                    response.SurveyServiceName,
                    MapStatus(response.Status),
                    response.OrderVersion,
                    response.RequiresBaselineMapping,
                    response.FarmBoundaryVersionId,
                    response.BoundaryCaptureSessionId,
                    response.BaselineRoutePlanId,
                    response.CurrentAppointmentId,
                    response.CurrentAppointmentVersion,
                    response.CurrentAppointmentStatus?.ToString().ToUpperInvariant(),
                    response.CreatedAt,
                    response.UpdatedAt))
                .ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);

    public static PagedResult<AssignedSurveyOrderApiResponse> ToResponse(
        PagedResult<AssignedSurveyOrderResponse> page) =>
        new(
            page.Items.Select(response => new AssignedSurveyOrderApiResponse(
                    response.SurveyOrderId,
                    response.OrderNumber,
                    response.TenantId,
                    response.FarmId,
                    response.FarmCode,
                    response.FarmName,
                    response.SurveyServiceId,
                    response.SurveyServiceCode,
                    response.SurveyServiceName,
                    MapStatus(response.Status),
                    response.OrderVersion,
                    response.RequiresBaselineMapping,
                    response.FarmBoundaryVersionId,
                    response.BoundaryCaptureSessionId,
                    response.BaselineRoutePlanId,
                    response.CurrentAppointmentId,
                    response.CurrentAppointmentVersion,
                    response.CurrentAppointmentStatus?.ToString().ToUpperInvariant(),
                    response.CreatedAt,
                    response.UpdatedAt,
                    response.CurrentAppointmentPurpose is { } purpose
                        ? SurveyAppointmentResponseMapper.MapPurpose(purpose)
                        : null,
                    response.CurrentAppointmentStartAt,
                    response.CurrentAppointmentEndAt))
                .ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);

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
                .ToArray(),
            response.ServiceReviewedAt);

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

    public static ConfirmSurveyOrderPoleCountApiResponse ToResponse(
        ConfirmSurveyOrderPoleCountResponse response) =>
        new(
            response.SurveyOrderId,
            response.OrderNumber,
            response.TenantId,
            response.FarmId,
            response.SurveyServiceId,
            response.FarmBoundaryVersionId,
            response.FarmBaseMapVersionId,
            response.PoleCountSource switch
            {
                SurveyPoleCountSource.BaselinePublication =>
                    "BASELINE_PUBLICATION",
                SurveyPoleCountSource.CurrentPublishedInventory =>
                    "CURRENT_PUBLISHED_INVENTORY",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(response),
                    response.PoleCountSource,
                    "Unsupported pole-count source.")
            },
            response.PoleCountEvidenceReference,
            response.ConfirmedSurveyPoleCount,
            response.SurveyServicePriceId,
            response.PricePerPole,
            response.Currency,
            response.FinalPrice,
            response.SystemManagerProfileId,
            MapStatus(response.OrderStatus),
            response.OrderVersion,
            response.ConfirmedAt,
            response.RemainingAmount,
            response.ConfirmedMappingPoleCount, response.ConfirmedPlantHealthPoleCount,
            response.MappingPricePerPole, response.PlantHealthPricePerPole,
            response.MappingSubtotal, response.PlantHealthSubtotal,
            response.ComboDiscountPercentage, response.ComboDiscountAmount);

    internal static string MapStatus(SurveyOrderStatus status) =>
        status switch
        {
            SurveyOrderStatus.PendingBoundaryVerification =>
                "PENDING_BOUNDARY_VERIFICATION",
            SurveyOrderStatus.AwaitingBaselineAppointment =>
                "AWAITING_BASELINE_APPOINTMENT",
            SurveyOrderStatus.AwaitingBaselineDeposit =>
                "AWAITING_BASELINE_DEPOSIT",
            SurveyOrderStatus.AwaitingBoundaryCapture =>
                "AWAITING_BOUNDARY_CAPTURE",
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
            SurveyOrderStatus.AwaitingAppointment => "AWAITING_APPOINTMENT",
            SurveyOrderStatus.AwaitingDeposit => "AWAITING_DEPOSIT",
            SurveyOrderStatus.ReadyForCapture => "READY_FOR_CAPTURE",
            SurveyOrderStatus.Capturing => "CAPTURING",
            SurveyOrderStatus.AwaitingCaptureUpload => "AWAITING_CAPTURE_UPLOAD",
            SurveyOrderStatus.AwaitingInputReview => "AWAITING_INPUT_REVIEW",
            SurveyOrderStatus.AwaitingProcessing => "AWAITING_PROCESSING",
            SurveyOrderStatus.Processing => "PROCESSING",
            SurveyOrderStatus.AwaitingReflightDecision => "AWAITING_REFLIGHT_DECISION",
            SurveyOrderStatus.AwaitingSettlement => "AWAITING_SETTLEMENT",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported SurveyOrder status.")
        };

    internal static SurveyOrderStatus? MapFilterStatus(
        SurveyOrderStatusFilterValue? status) =>
        status switch
        {
            null => null,
            SurveyOrderStatusFilterValue.PENDING_BOUNDARY_VERIFICATION =>
                SurveyOrderStatus.PendingBoundaryVerification,
            SurveyOrderStatusFilterValue.AWAITING_BASELINE_APPOINTMENT =>
                SurveyOrderStatus.AwaitingBaselineAppointment,
            SurveyOrderStatusFilterValue.AWAITING_BASELINE_DEPOSIT =>
                SurveyOrderStatus.AwaitingBaselineDeposit,
            SurveyOrderStatusFilterValue.AWAITING_BOUNDARY_CAPTURE =>
                SurveyOrderStatus.AwaitingBoundaryCapture,
            SurveyOrderStatusFilterValue.BASELINE_READY =>
                SurveyOrderStatus.BaselineReady,
            SurveyOrderStatusFilterValue.BASELINE_IN_PROGRESS =>
                SurveyOrderStatus.BaselineInProgress,
            SurveyOrderStatusFilterValue.AWAITING_BASELINE_REVIEW =>
                SurveyOrderStatus.AwaitingBaselineReview,
            SurveyOrderStatusFilterValue.AWAITING_PRICING =>
                SurveyOrderStatus.AwaitingPricing,
            SurveyOrderStatusFilterValue.AWAITING_PAID_APPOINTMENT =>
                SurveyOrderStatus.AwaitingPaidAppointment,
            SurveyOrderStatusFilterValue.AWAITING_PAYMENT =>
                SurveyOrderStatus.AwaitingPayment,
            SurveyOrderStatusFilterValue.READY_FOR_PAID_SERVICE =>
                SurveyOrderStatus.ReadyForPaidService,
            SurveyOrderStatusFilterValue.IN_PROGRESS =>
                SurveyOrderStatus.InProgress,
            SurveyOrderStatusFilterValue.PENDING_REVIEW =>
                SurveyOrderStatus.PendingReview,
            SurveyOrderStatusFilterValue.COMPLETED =>
                SurveyOrderStatus.Completed,
            SurveyOrderStatusFilterValue.CANCELLED =>
                SurveyOrderStatus.Cancelled,
            SurveyOrderStatusFilterValue.AWAITING_APPOINTMENT =>
                SurveyOrderStatus.AwaitingAppointment,
            SurveyOrderStatusFilterValue.AWAITING_DEPOSIT =>
                SurveyOrderStatus.AwaitingDeposit,
            SurveyOrderStatusFilterValue.READY_FOR_CAPTURE =>
                SurveyOrderStatus.ReadyForCapture,
            SurveyOrderStatusFilterValue.CAPTURING =>
                SurveyOrderStatus.Capturing,
            SurveyOrderStatusFilterValue.AWAITING_CAPTURE_UPLOAD =>
                SurveyOrderStatus.AwaitingCaptureUpload,
            SurveyOrderStatusFilterValue.AWAITING_INPUT_REVIEW =>
                SurveyOrderStatus.AwaitingInputReview,
            SurveyOrderStatusFilterValue.AWAITING_PROCESSING =>
                SurveyOrderStatus.AwaitingProcessing,
            SurveyOrderStatusFilterValue.PROCESSING =>
                SurveyOrderStatus.Processing,
            SurveyOrderStatusFilterValue.AWAITING_REFLIGHT_DECISION =>
                SurveyOrderStatus.AwaitingReflightDecision,
            SurveyOrderStatusFilterValue.AWAITING_SETTLEMENT =>
                SurveyOrderStatus.AwaitingSettlement,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported survey-order status.")
        };
}
