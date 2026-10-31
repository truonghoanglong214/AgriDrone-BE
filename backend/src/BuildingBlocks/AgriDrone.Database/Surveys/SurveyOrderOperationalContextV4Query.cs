using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderOperationalContextV4Query(
    AgriDroneSchemaDbContext db,
    ISystemManagerAccessService managerAccess,
    TimeProvider clock) : ISurveyOrderOperationalContextV4Query
{
    public async Task<SurveyOrderOperationalContextV4?> GetAsync(
        Guid surveyOrderId,
        string requestedMissionPurpose,
        CancellationToken cancellationToken = default)
    {
        var purpose = requestedMissionPurpose switch
        {
            nameof(SurveyOperationPurpose.BaselineMapping) => SurveyOperationPurpose.BaselineMapping,
            nameof(SurveyOperationPurpose.PlantHealth) => SurveyOperationPurpose.PlantHealth,
            nameof(SurveyOperationPurpose.HarvestReadiness) => SurveyOperationPurpose.HarvestReadiness,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedMissionPurpose))
        };

        var order = await db.Set<SurveyOrder>().AsNoTracking()
            .Include(value => value.SurveyService)
            .Include(value => value.Appointments)
            .Include(value => value.Payments)
            .SingleOrDefaultAsync(value => value.Id == surveyOrderId, cancellationToken);

        if (order is null) return null;

        var farm = await db.Set<Farm>().AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == order.FarmId && f.TenantId == order.TenantId, cancellationToken);
        var candidateCenterLongitude = farm?.CenterPoint?.X;
        var candidateCenterLatitude = farm?.CenterPoint?.Y;
        var approximateAreaHa = farm?.AreaHectares;
        double? surveyRadiusMeters = null;
        if (candidateCenterLongitude.HasValue && candidateCenterLatitude.HasValue)
        {
            if (approximateAreaHa.HasValue && approximateAreaHa.Value > 0)
            {
                surveyRadiusMeters = Math.Round(Math.Sqrt((double)approximateAreaHa.Value * 10000.0 / Math.PI) * 1.2, 1);
            }
            else
            {
                surveyRadiusMeters = 150.0;
            }
        }

        var appointmentPurpose = purpose == SurveyOperationPurpose.BaselineMapping
            ? SurveyAppointmentPurpose.BaselineMapping
            : SurveyAppointmentPurpose.PaidService;

        var appointment = order.Appointments
            .Where(value => value.Purpose == appointmentPurpose)
            .OrderByDescending(value => value.UpdatedAt)
            .FirstOrDefault();

        var paymentPurpose = purpose == SurveyOperationPurpose.BaselineMapping
            ? PaymentPurpose.BaselineDeposit
            : PaymentPurpose.FinalBalance;

        var payment = order.Payments
            .Where(value => value.Purpose == paymentPurpose)
            .OrderByDescending(value => value.UpdatedAt)
            .FirstOrDefault();

        var access = await managerAccess.ResolveFarmAccessAsync(order.FarmId, cancellationToken);
        var managerReady = access.IsAllowed && access.TenantId == order.TenantId;

        // Boundary version details
        FarmBoundary? boundary = null;
        if (order.FarmBoundaryVersionId is Guid boundaryId)
        {
            boundary = await db.Set<FarmBoundary>().AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.Id == boundaryId &&
                    value.TenantId == order.TenantId &&
                    value.FarmId == order.FarmId &&
                    value.Status == FarmBoundaryStatus.Approved,
                    cancellationToken);
        }
        var boundaryApproved = boundary is not null;

        // Base map version details
        FarmBaseMapVersion? baseMap = null;
        if (order.FarmBaseMapVersionId is Guid mapId)
        {
            baseMap = await db.Set<FarmBaseMapVersion>().AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.Id == mapId &&
                    value.TenantId == order.TenantId &&
                    value.FarmId == order.FarmId &&
                    value.Status == FarmBaseMapStatus.Published,
                    cancellationToken);
        }
        var mapPublished = baseMap is not null;

        var evaluatedAt = clock.GetUtcNow();

        // Adjustments
        var priceAdjustments = await db.Set<PriceAdjustment>().AsNoTracking()
            .Where(value => value.SurveyOrderId == order.Id)
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var adjustmentsSummary = priceAdjustments.Select(adj => new OperationalAdjustmentDetail(
            adj.Id,
            adj.Reason,
            adj.Status.ToString(),
            adj.OldFinalPrice,
            adj.NewFinalPrice,
            adj.Currency,
            adj.Version,
            adj.CreatedAt)).ToList();

        var hasPendingAdjustment = priceAdjustments.Any(adj =>
            adj.Status == PriceAdjustmentStatus.Pending ||
            adj.Status == PriceAdjustmentStatus.Approved);

        // Settlements
        var adjustmentIds = priceAdjustments.Select(adj => adj.Id).ToList();
        var settlements = await db.Set<AdjustmentSettlement>().AsNoTracking()
            .Where(value => adjustmentIds.Contains(value.PriceAdjustmentId))
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var settlementsSummary = settlements.Select(stl => new OperationalSettlementDetail(
            stl.Id,
            stl.PriceAdjustmentId,
            stl.Type.ToString(),
            stl.Status.ToString(),
            stl.Amount,
            stl.Currency,
            stl.FailureReason,
            stl.Version,
            stl.CreatedAt)).ToList();

        var hasPendingSettlement = settlements.Any(stl =>
            stl.Status == AdjustmentSettlementStatus.Pending ||
            stl.Status == AdjustmentSettlementStatus.Processing ||
            stl.Status == AdjustmentSettlementStatus.Failed);

        // Extra flights
        var extraFlights = await db.Set<ExtraFlightRequest>().AsNoTracking()
            .Where(value => value.SurveyOrderId == order.Id)
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        var extraFlightsSummary = extraFlights.Select(ef => new OperationalExtraFlightDetail(
            ef.Id,
            ef.Reason.ToString(),
            ef.Status.ToString(),
            ef.CostImpact,
            ef.Currency,
            ef.Version,
            ef.CreatedAt)).ToList();

        var hasUnapprovedExtraFlight = extraFlights.Any(ef =>
            ef.Status == ExtraFlightRequestStatus.Pending);

        // Boundary capture session
        BoundaryCaptureSession? captureSession = null;
        if (order.BoundaryCaptureSessionId.HasValue)
        {
            captureSession = await db.Set<BoundaryCaptureSession>().AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.Id == order.BoundaryCaptureSessionId.Value &&
                    value.SurveyOrderId == order.Id,
                    cancellationToken);
        }

        var hasFinalizedBoundaryCapture = captureSession is not null &&
            captureSession.FarmBoundaryVersionId == order.FarmBoundaryVersionId &&
            captureSession.Status == BoundaryCaptureSessionStatus.Finalized;

        // Baseline route plan reference
        BaselineRoutePlanReference? routePlanRef = null;
        if (order.BaselineRoutePlanId.HasValue)
        {
            routePlanRef = await db.Set<BaselineRoutePlanReference>().AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.RoutePlanId == order.BaselineRoutePlanId.Value &&
                    value.SurveyOrderId == order.Id,
                    cancellationToken);
        }

        var hasApprovedBaselineRoute = routePlanRef is not null &&
            routePlanRef.Status == BaselineRoutePlanReferenceStatus.Approved;

        var isBaselineRouteCurrent = routePlanRef is not null &&
            routePlanRef.BoundaryCaptureSessionId == order.BoundaryCaptureSessionId &&
            routePlanRef.FarmBoundaryVersionId == order.FarmBoundaryVersionId;

        var isBaselineRouteWithinSafetyInset = routePlanRef is not null &&
            routePlanRef.IsWithinSafetyInset;

        var scopeZoneIds = await db.Set<FarmZone>().AsNoTracking()
            .Where(z => z.FarmId == order.FarmId && z.Status == GeneralStatus.Active && z.DeletedAt == null)
            .OrderBy(z => z.Code)
            .Select(z => z.Id)
            .ToListAsync(cancellationToken);

        var expectedPayableAmount = order.RemainingAmount ?? order.FinalPrice;
        var isPaymentAmountAndCurrencyMatched = purpose == SurveyOperationPurpose.BaselineMapping
            || (payment is not null
                && expectedPayableAmount.HasValue
                && payment.Amount == expectedPayableAmount.Value
                && string.Equals(payment.Currency, order.Currency, StringComparison.OrdinalIgnoreCase));

        // Commercial Readiness evaluation (BE1 authoritative)
        var commercialSnapshot = new SurveyOrderReadinessSnapshot(
            OrderStatus: order.Status,
            Purpose: purpose,
            RequiresBaselineMapping: order.RequiresBaselineMapping,
            HasApprovedFarmBoundary: boundaryApproved,
            IsScopeConfirmed: order.ScopeConfirmedAt.HasValue,
            HasPublishedFarmBaseMap: mapPublished,
            HasConfirmedPoleCount: order.ConfirmedSurveyPoleCount.HasValue,
            HasPriceSnapshot: order.PricePerPoleSnapshot.HasValue && order.SurveyServicePriceId.HasValue,
            AppointmentPurpose: appointment?.Purpose,
            AppointmentStatus: appointment?.Status,
            PaymentStatus: payment?.Status,
            HasActiveQualifiedPrimaryManager: managerReady,
            IsPrimaryManagerAvailable: managerReady,
            IsAppointmentCurrent: appointment?.Status == SurveyAppointmentStatus.Confirmed,
            IsMissionPreparedForPurpose: false,
            IsMissionScheduledWithinAppointment: false,
            IsDroneReady: false,
            IsPreflightCompleted: false,
            HasPendingPriceAdjustment: hasPendingAdjustment,
            IsPaymentAmountAndCurrencyMatched: isPaymentAmountAndCurrencyMatched,
            SafetyChecksSatisfied: false,
            HasPendingFinancialSettlement: hasPendingSettlement,
            HasUnapprovedExtraFlight: hasUnapprovedExtraFlight,
            HasFinalizedBoundaryCapture: hasFinalizedBoundaryCapture,
            HasApprovedBaselineRoute: hasApprovedBaselineRoute,
            IsBaselineRouteCurrent: isBaselineRouteCurrent,
            IsBaselineRouteWithinSafetyInset: isBaselineRouteWithinSafetyInset,
            DepositPaymentStatus: order.DepositPaymentStatus);

        var commercialDecision = SurveyOrderReadinessPolicy.EvaluateCommercialReadiness(commercialSnapshot);

        var managerContext = new PrimaryManagerOperationalContext(
            ManagerProfileId: managerReady ? access.ManagerProfileId : null,
            AssignmentId: null,
            AssignmentVersion: null,
            IsActive: managerReady,
            IsAvailable: managerReady);

        var commercialReadiness = new CommercialReadinessEvaluation(
            commercialDecision.IsReady,
            commercialDecision.Failures.Select(f => f.ToString()).ToList());

        // In BE1 DB context, mission/drone readiness is unconfirmed until BE2 confirms execution status
        var missionReadiness = new OperationalMissionReadiness(
            IsMissionPrepared: false,
            IsMissionScheduled: false,
            IsDroneReady: false,
            IsPreflightCompleted: false,
            SafetyChecksSatisfied: false);

        return new SurveyOrderOperationalContextV4(
            SurveyOrderId: order.Id,
            TenantId: order.TenantId,
            FarmId: order.FarmId,
            ServiceCode: order.SurveyService.Code,
            ServiceType: order.SurveyService.ServiceType.ToString(),
            RequestedMissionPurpose: purpose.ToString(),
            OrderStatus: order.Status.ToString(),
            OrderVersion: order.Version,
            FarmBoundaryVersionId: order.FarmBoundaryVersionId,
            FarmBoundaryVersionNumber: boundary?.VersionNumber,
            FarmBaseMapVersionId: order.FarmBaseMapVersionId,
            FarmBaseMapVersionNumber: baseMap?.VersionNumber,
            RequiresBaselineMapping: order.RequiresBaselineMapping,
            ScopeZoneIds: scopeZoneIds,
            ScopeConfirmedAt: order.ScopeConfirmedAt,
            OriginalSurveyPoleCount: order.OriginalConfirmedPoleCount?.Value,
            ConfirmedSurveyPoleCount: order.ConfirmedSurveyPoleCount?.Value,
            SurveyServicePriceId: order.SurveyServicePriceId,
            OriginalPricePerPole: order.OriginalPricePerPoleSnapshot?.Amount,
            PricePerPole: order.PricePerPoleSnapshot?.Amount,
            Currency: order.Currency,
            OriginalTotalPrice: order.OriginalFinalPrice,
            FinalPrice: order.FinalPrice,
            AppointmentId: appointment?.Id,
            AppointmentPurpose: appointment?.Purpose.ToString(),
            AppointmentStartAt: appointment?.ProposedStartAt,
            AppointmentEndAt: appointment?.ProposedEndAt,
            AppointmentStatus: appointment?.Status.ToString(),
            AppointmentVersion: appointment?.Version,
            PaymentId: payment?.Id,
            PaymentStatus: payment?.Status.ToString(),
            PaidAmount: payment?.Amount,
            PaidCurrency: payment?.Currency,
            IsPaymentAmountAndCurrencyMatched: isPaymentAmountAndCurrencyMatched,
            ManagerContext: managerContext,
            PreviousCompatibleOrderId: order.PreviousCompatibleOrderId,
            BoundaryCaptureSessionId: order.BoundaryCaptureSessionId,
            BoundaryCaptureSessionVersion: captureSession is not null ? (int)captureSession.Version : null,
            BoundaryCaptureSessionStatus: captureSession?.Status.ToString(),
            BaselineRoutePlanId: order.BaselineRoutePlanId,
            BaselineRoutePlanVersion: routePlanRef?.RoutePlanVersion,
            BaselineRoutePlanStatus: routePlanRef?.Status.ToString(),
            IsBaselineRouteCurrent: isBaselineRouteCurrent,
            IsBaselineRouteWithinSafetyInset: isBaselineRouteWithinSafetyInset,
            HasPendingAdjustment: hasPendingAdjustment,
            HasPendingSettlement: hasPendingSettlement,
            HasUnapprovedExtraFlight: hasUnapprovedExtraFlight,
            Adjustments: adjustmentsSummary,
            Settlements: settlementsSummary,
            ExtraFlights: extraFlightsSummary,
            CommercialReadiness: commercialReadiness,
            MissionReadiness: missionReadiness,
            EvaluatedAt: evaluatedAt,
            DepositPaymentStatus: order.DepositPaymentStatus.ToString(),
            DepositAmount: order.DepositAmount,
            RemainingAmount: order.RemainingAmount,
            DepositPaymentId: order.DepositPaymentId,
            DepositConfirmedAt: order.DepositConfirmedAt,
            CandidateCenterLongitude: candidateCenterLongitude,
            CandidateCenterLatitude: candidateCenterLatitude,
            ApproximateAreaHa: approximateAreaHa,
            SurveyRadiusMeters: surveyRadiusMeters);
    }
}
