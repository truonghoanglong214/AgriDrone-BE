using AgriDrone.IntegrationContracts.Surveys;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class V4OrderPlanningContractTests
{
    [Fact]
    public void V4ContractPreservesAllOperationalDetailsAndSeparatedReadiness()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        var managerContext = new PrimaryManagerOperationalContext(
            ManagerProfileId: Guid.NewGuid(),
            AssignmentId: Guid.NewGuid(),
            AssignmentVersion: 1,
            IsActive: true,
            IsAvailable: true);

        var adjustments = new List<OperationalAdjustmentDetail>
        {
            new(
                Guid.NewGuid(),
                "Pole count increase",
                "Approved",
                1000m,
                1500m,
                "VND",
                1,
                now.AddDays(-1))
        };

        var settlements = new List<OperationalSettlementDetail>
        {
            new(
                Guid.NewGuid(),
                adjustments[0].Id,
                "AdditionalPayment",
                "Confirmed",
                500m,
                "VND",
                null,
                1,
                now.AddDays(-1))
        };

        var extraFlights = new List<OperationalExtraFlightDetail>
        {
            new(
                Guid.NewGuid(),
                "ApprovedScopeChange",
                "Approved",
                200m,
                "VND",
                1,
                now.AddDays(-1))
        };

        var commercialReadiness = new CommercialReadinessEvaluation(
            IsReady: true,
            Failures: []);

        var missionReadiness = new OperationalMissionReadiness(
            IsMissionPrepared: true,
            IsMissionScheduled: true,
            IsDroneReady: true,
            IsPreflightCompleted: true,
            SafetyChecksSatisfied: true);

        var context = new SurveyOrderOperationalContextV4(
            SurveyOrderId: orderId,
            TenantId: tenantId,
            FarmId: farmId,
            ServiceCode: "PLANT_HEALTH",
            ServiceType: "PlantHealth",
            RequestedMissionPurpose: "PlantHealth",
            OrderStatus: "ReadyForPaidService",
            OrderVersion: 2,
            FarmBoundaryVersionId: boundaryId,
            FarmBoundaryVersionNumber: 3,
            FarmBaseMapVersionId: Guid.NewGuid(),
            FarmBaseMapVersionNumber: 1,
            RequiresBaselineMapping: false,
            ScopeZoneIds: [Guid.NewGuid(), Guid.NewGuid()],
            ScopeConfirmedAt: now.AddDays(-2),
            OriginalSurveyPoleCount: 100,
            ConfirmedSurveyPoleCount: 150,
            SurveyServicePriceId: Guid.NewGuid(),
            OriginalPricePerPole: 10m,
            PricePerPole: 10m,
            Currency: "VND",
            OriginalTotalPrice: 1000m,
            FinalPrice: 1500m,
            AppointmentId: appointmentId,
            AppointmentPurpose: "PaidService",
            AppointmentStartAt: now.AddHours(1),
            AppointmentEndAt: now.AddHours(3),
            AppointmentStatus: "Confirmed",
            AppointmentVersion: 1,
            PaymentId: Guid.NewGuid(),
            PaymentStatus: "Confirmed",
            PaidAmount: 1500m,
            PaidCurrency: "VND",
            IsPaymentAmountAndCurrencyMatched: true,
            ManagerContext: managerContext,
            PreviousCompatibleOrderId: null,
            BoundaryCaptureSessionId: null,
            BoundaryCaptureSessionVersion: null,
            BoundaryCaptureSessionStatus: null,
            BaselineRoutePlanId: null,
            BaselineRoutePlanVersion: null,
            BaselineRoutePlanStatus: null,
            IsBaselineRouteCurrent: false,
            IsBaselineRouteWithinSafetyInset: false,
            HasPendingAdjustment: false,
            HasPendingSettlement: false,
            HasUnapprovedExtraFlight: false,
            Adjustments: adjustments,
            Settlements: settlements,
            ExtraFlights: extraFlights,
            CommercialReadiness: commercialReadiness,
            MissionReadiness: missionReadiness,
            EvaluatedAt: now);

        Assert.Equal(orderId, context.SurveyOrderId);
        Assert.Equal(3, context.FarmBoundaryVersionNumber);
        Assert.Equal(100, context.OriginalSurveyPoleCount);
        Assert.Equal(150, context.ConfirmedSurveyPoleCount);
        Assert.Equal(1000m, context.OriginalTotalPrice);
        Assert.Equal(1500m, context.FinalPrice);
        Assert.True(context.IsPaymentAmountAndCurrencyMatched);
        Assert.True(context.CommercialReadiness.IsReady);
        Assert.True(context.MissionReadiness.IsDroneReady);
        Assert.Single(context.Adjustments);
        Assert.Single(context.Settlements);
        Assert.Single(context.ExtraFlights);
        Assert.True(context.ManagerContext.IsActive);
        Assert.True(context.ManagerContext.IsAvailable);
    }
}
