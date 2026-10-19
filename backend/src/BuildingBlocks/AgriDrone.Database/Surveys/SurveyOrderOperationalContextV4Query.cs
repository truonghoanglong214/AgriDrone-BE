using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Farms.Domain.Boundaries;
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

        var appointmentPurpose = purpose == SurveyOperationPurpose.BaselineMapping
            ? SurveyAppointmentPurpose.BaselineMapping
            : SurveyAppointmentPurpose.PaidService;

        var appointment = order.Appointments
            .Where(value => value.Purpose == appointmentPurpose)
            .OrderByDescending(value => value.UpdatedAt)
            .FirstOrDefault();

        var payment = order.Payments
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

        // Consent and flight permission authorizations
        var consentAuth = await db.Set<SurveyCaptureAuthorization>().AsNoTracking()
            .Where(value =>
                value.SurveyOrderId == order.Id &&
                value.Type == SurveyCaptureAuthorizationType.WrittenDataUseConsent &&
                value.Status == SurveyCaptureAuthorizationStatus.Verified &&
                value.VerifiedAt <= evaluatedAt &&
                (!value.ExpiresAt.HasValue || evaluatedAt < value.ExpiresAt.Value) &&
                !value.RevokedAt.HasValue)
            .OrderByDescending(value => value.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var flightPermissionAuth = await db.Set<SurveyCaptureAuthorization>().AsNoTracking()
            .Where(value =>
                value.SurveyOrderId == order.Id &&
                value.Type == SurveyCaptureAuthorizationType.FlightPermission &&
                value.Status == SurveyCaptureAuthorizationStatus.Verified &&
                value.VerifiedAt <= evaluatedAt &&
                (!value.ExpiresAt.HasValue || evaluatedAt < value.ExpiresAt.Value) &&
                !value.RevokedAt.HasValue)
            .OrderByDescending(value => value.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

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

        // Authoritative payment amount & currency match
        var isPaymentAmountAndCurrencyMatched = purpose == SurveyOperationPurpose.BaselineMapping
            || (payment is not null
                && order.FinalPrice.HasValue
                && payment.Amount == order.FinalPrice.Value
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
            HasValidWrittenDataUseConsent: consentAuth is not null,
            HasValidFlightPermission: flightPermissionAuth is not null,
            HasPendingFinancialSettlement: hasPendingSettlement,
            HasUnapprovedExtraFlight: hasUnapprovedExtraFlight,
            HasFinalizedBoundaryCapture: hasFinalizedBoundaryCapture,
            HasApprovedBaselineRoute: hasApprovedBaselineRoute,
            IsBaselineRouteCurrent: isBaselineRouteCurrent,
            IsBaselineRouteWithinSafetyInset: isBaselineRouteWithinSafetyInset);

        var commercialDecision = SurveyOrderReadinessPolicy.EvaluateCommercialReadiness(commercialSnapshot);

        var managerContext = new PrimaryManagerOperationalContext(
            ManagerProfileId: managerReady ? access.ManagerProfileId : null,
            AssignmentId: null,
            AssignmentVersion: null,
            IsActive: managerReady,
            IsQualified: managerReady,
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
            OriginalSurveyPoleCount: order.ConfirmedSurveyPoleCount?.Value,
            ConfirmedSurveyPoleCount: order.ConfirmedSurveyPoleCount?.Value,
            SurveyServicePriceId: order.SurveyServicePriceId,
            OriginalPricePerPole: order.PricePerPoleSnapshot?.Amount,
            PricePerPole: order.PricePerPoleSnapshot?.Amount,
            Currency: order.Currency,
            OriginalTotalPrice: order.FinalPrice,
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
            HasValidWrittenDataUseConsent: consentAuth is not null,
            WrittenDataUseConsentId: consentAuth?.Id,
            HasValidFlightPermission: flightPermissionAuth is not null,
            FlightPermissionId: flightPermissionAuth?.Id,
            Adjustments: adjustmentsSummary,
            Settlements: settlementsSummary,
            ExtraFlights: extraFlightsSummary,
            CommercialReadiness: commercialReadiness,
            MissionReadiness: missionReadiness,
            EvaluatedAt: evaluatedAt);
    }
}
