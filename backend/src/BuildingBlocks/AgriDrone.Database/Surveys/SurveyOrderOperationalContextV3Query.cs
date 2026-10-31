using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderOperationalContextV3Query(
    AgriDroneSchemaDbContext db,
    ISystemManagerAccessService managerAccess,
    TimeProvider clock) : ISurveyOrderOperationalContextV3Query
{
    public async Task<SurveyOrderOperationalContextV3?> GetAsync(
        Guid surveyOrderId, string requestedMissionPurpose,
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
            .Include(value => value.ZoneScopes)
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
        var boundaryApproved = order.FarmBoundaryVersionId is Guid boundaryId &&
            await db.Set<FarmBoundary>().AsNoTracking().AnyAsync(value =>
                value.Id == boundaryId && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.Status == FarmBoundaryStatus.Approved,
                cancellationToken);
        var mapPublished = order.FarmBaseMapVersionId is Guid mapId &&
            await db.Set<FarmBaseMapVersion>().AsNoTracking().AnyAsync(value =>
                value.Id == mapId && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.Status == FarmBaseMapStatus.Published,
                cancellationToken);
        var pendingAdjustment = await db.Set<PriceAdjustment>().AsNoTracking().AnyAsync(value =>
            value.SurveyOrderId == order.Id &&
            (value.Status == PriceAdjustmentStatus.Pending ||
             value.Status == PriceAdjustmentStatus.Approved), cancellationToken);

        var hasPendingSettlement = await db.Set<AdjustmentSettlement>().AsNoTracking().AnyAsync(value =>
            db.Set<PriceAdjustment>().Any(adj => adj.Id == value.PriceAdjustmentId && adj.SurveyOrderId == order.Id) &&
            (value.Status == AdjustmentSettlementStatus.Pending ||
             value.Status == AdjustmentSettlementStatus.Processing ||
             value.Status == AdjustmentSettlementStatus.Failed),
            cancellationToken);

        var hasUnapprovedExtraFlight = await db.Set<ExtraFlightRequest>().AsNoTracking().AnyAsync(value =>
            value.SurveyOrderId == order.Id &&
            value.Status == ExtraFlightRequestStatus.Pending,
            cancellationToken);

        var hasFinalizedBoundaryCapture = order.BoundaryCaptureSessionId.HasValue &&
            await db.Set<BoundaryCaptureSession>().AsNoTracking().AnyAsync(value =>
                value.Id == order.BoundaryCaptureSessionId.Value &&
                value.SurveyOrderId == order.Id &&
                value.FarmBoundaryVersionId == order.FarmBoundaryVersionId &&
                value.Status == BoundaryCaptureSessionStatus.Finalized,
                cancellationToken);

        var hasApprovedBaselineRoute = order.BaselineRoutePlanId.HasValue &&
            await db.Set<BaselineRoutePlanReference>().AsNoTracking().AnyAsync(value =>
                value.RoutePlanId == order.BaselineRoutePlanId.Value &&
                value.SurveyOrderId == order.Id &&
                value.Status == BaselineRoutePlanReferenceStatus.Approved,
                cancellationToken);

        var isBaselineRouteCurrent = order.BaselineRoutePlanId.HasValue &&
            await db.Set<BaselineRoutePlanReference>().AsNoTracking().AnyAsync(value =>
                value.RoutePlanId == order.BaselineRoutePlanId.Value &&
                value.SurveyOrderId == order.Id &&
                value.BoundaryCaptureSessionId == order.BoundaryCaptureSessionId &&
                value.FarmBoundaryVersionId == order.FarmBoundaryVersionId,
                cancellationToken);

        var isBaselineRouteWithinSafetyInset = order.BaselineRoutePlanId.HasValue &&
            await db.Set<BaselineRoutePlanReference>().AsNoTracking().AnyAsync(value =>
                value.RoutePlanId == order.BaselineRoutePlanId.Value &&
                value.SurveyOrderId == order.Id &&
                value.IsWithinSafetyInset,
                cancellationToken);

        string? captureSessionStatus = null;
        if (order.BoundaryCaptureSessionId.HasValue)
        {
            captureSessionStatus = await db.Set<BoundaryCaptureSession>().AsNoTracking()
                .Where(s => s.Id == order.BoundaryCaptureSessionId.Value)
                .Select(s => s.Status.ToString())
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? baselineRoutePlanStatus = null;
        if (order.BaselineRoutePlanId.HasValue)
        {
            baselineRoutePlanStatus = await db.Set<BaselineRoutePlanReference>().AsNoTracking()
                .Where(r => r.RoutePlanId == order.BaselineRoutePlanId.Value)
                .Select(r => r.Status.ToString())
                .FirstOrDefaultAsync(cancellationToken);
        }

        var isBaselineScope = purpose == SurveyOperationPurpose.BaselineMapping;
        var scopeType = isBaselineScope
            ? IntegrationContracts.Surveys.SurveyScopeType.FarmWide
            : order.ScopeType == Modules.Surveys.Domain.SurveyScopeType.Zones
                ? IntegrationContracts.Surveys.SurveyScopeType.Zones
                : IntegrationContracts.Surveys.SurveyScopeType.FarmWide;
        var scopeZones = isBaselineScope
            ? []
            : order.ZoneScopes
                .OrderBy(scope => scope.ZoneId)
                .Select(scope => new SurveyScopeZoneV3(
                    scope.ZoneId,
                    scope.ZoneVersion,
                    ToGeoJson(scope.BoundarySnapshot)))
                .ToArray();
        var scopeZoneIds = scopeZones.Select(scope => scope.ZoneId).ToArray();
        var currentScopeVersions = scopeZoneIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await db.Set<FarmZone>().AsNoTracking()
                .Where(zone => scopeZoneIds.Contains(zone.Id) &&
                               zone.FarmId == order.FarmId &&
                               zone.Status == GeneralStatus.Active &&
                               zone.DeletedAt == null &&
                               zone.Boundary != null)
                .ToDictionaryAsync(zone => zone.Id, zone => zone.Version,
                    cancellationToken);
        var scopeStale = scopeType == IntegrationContracts.Surveys.SurveyScopeType.Zones &&
            scopeZones.Any(scope =>
                !currentScopeVersions.TryGetValue(scope.ZoneId, out var version) ||
                version != scope.ZoneVersion);

        var expectedPayableAmount = order.RemainingAmount ?? order.FinalPrice;
        var isPaymentAmountAndCurrencyMatched = purpose == SurveyOperationPurpose.BaselineMapping
            || (payment is not null
                && expectedPayableAmount.HasValue
                && payment.Amount == expectedPayableAmount.Value
                && string.Equals(payment.Currency, order.Currency, StringComparison.OrdinalIgnoreCase));

        var safetyChecksSatisfied = (purpose == SurveyOperationPurpose.BaselineMapping || boundaryApproved) && managerReady;

        var snapshot = new SurveyOrderReadinessSnapshot(
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
            IsMissionPreparedForPurpose: safetyChecksSatisfied,
            IsMissionScheduledWithinAppointment: safetyChecksSatisfied,
            IsDroneReady: safetyChecksSatisfied,
            IsPreflightCompleted: safetyChecksSatisfied,
            HasPendingPriceAdjustment: pendingAdjustment,
            IsPaymentAmountAndCurrencyMatched: isPaymentAmountAndCurrencyMatched,
            SafetyChecksSatisfied: safetyChecksSatisfied,
            HasPendingFinancialSettlement: hasPendingSettlement,
            HasUnapprovedExtraFlight: hasUnapprovedExtraFlight,
            HasFinalizedBoundaryCapture: hasFinalizedBoundaryCapture,
            HasApprovedBaselineRoute: hasApprovedBaselineRoute,
            IsBaselineRouteCurrent: isBaselineRouteCurrent,
            IsBaselineRouteWithinSafetyInset: isBaselineRouteWithinSafetyInset,
            DepositPaymentStatus: order.DepositPaymentStatus);

        var decision = SurveyOrderReadinessPolicy.EvaluateCommercialReadiness(snapshot);

        var readinessFailures = decision.Failures
            .Select(value => value.ToString())
            .ToList();
        if (scopeStale)
        {
            readinessFailures.Insert(0, "SCOPE_STALE");
        }

        return new SurveyOrderOperationalContextV3(
            order.Id, order.TenantId, order.FarmId, order.SurveyService.Code,
            order.SurveyService.ServiceType.ToString(), purpose.ToString(),
            order.Status.ToString(), order.Version, order.FarmBoundaryVersionId,
            order.FarmBaseMapVersionId, order.RequiresBaselineMapping,
            order.ConfirmedSurveyPoleCount?.Value, order.SurveyServicePriceId,
            order.PricePerPoleSnapshot?.Amount, order.Currency, order.FinalPrice,
            appointment?.Id, appointment?.Purpose.ToString(),
            appointment?.ProposedStartAt, appointment?.ProposedEndAt,
            appointment?.Status.ToString(), payment?.Id, payment?.Status.ToString(),
            managerReady ? access.ManagerProfileId : null,
            order.PreviousCompatibleOrderId,
            decision.IsReady && !scopeStale, readinessFailures,
            clock.GetUtcNow(),
            scopeZoneIds,
            order.BoundaryCaptureSessionId,
            captureSessionStatus,
            order.BaselineRoutePlanId,
            baselineRoutePlanStatus,
            hasPendingSettlement,
            hasUnapprovedExtraFlight,
            pendingAdjustment,
            order.DepositPaymentStatus.ToString(),
            order.DepositAmount,
            order.RemainingAmount,
            order.DepositPaymentId,
            order.DepositConfirmedAt,
            candidateCenterLongitude,
            candidateCenterLatitude,
            approximateAreaHa,
            surveyRadiusMeters,
            scopeType,
            scopeZones);
    }

    private static GeoJsonPolygonV3 ToGeoJson(
        NetTopologySuite.Geometries.Polygon polygon)
    {
        var rings = new List<IReadOnlyList<IReadOnlyList<double>>>
        {
            polygon.ExteriorRing.Coordinates
                .Select(coordinate => (IReadOnlyList<double>)
                    [coordinate.X, coordinate.Y])
                .ToArray()
        };
        for (var i = 0; i < polygon.NumInteriorRings; i++)
        {
            rings.Add(polygon.GetInteriorRingN(i).Coordinates
                .Select(coordinate => (IReadOnlyList<double>)
                    [coordinate.X, coordinate.Y])
                .ToArray());
        }

        return new GeoJsonPolygonV3("Polygon", rings);
    }
}
