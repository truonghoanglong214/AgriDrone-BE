using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyOrderReadinessPolicyTests
{
    [Fact]
    public void EvaluateReturnsFailureWhenPurposeIsHarvestReadiness()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.HarvestReadiness);

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.ServiceDisabled, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenBaselineMappingDepositNotConfirmed()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping) with
        {
            DepositPaymentStatus = SurveyPaymentStatus.Pending
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.PaymentNotConfirmed, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenPlantHealthBoundaryNotApproved()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth) with
        {
            HasApprovedFarmBoundary = false
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.BoundaryNotApproved, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenPlantHealthScopeNotConfirmed()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth) with
        {
            IsScopeConfirmed = false
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.ScopeNotConfirmed, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenConsentNotValid()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping) with
        {
            HasValidWrittenDataUseConsent = false
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.WrittenConsentNotValid, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenFlightPermissionNotValid()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping) with
        {
            HasValidFlightPermission = false
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.FlightPermissionNotValid, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenPendingFinancialSettlementExists()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping) with
        {
            HasPendingFinancialSettlement = true
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.FinancialSettlementPending, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenUnapprovedExtraFlightExists()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping) with
        {
            HasUnapprovedExtraFlight = true
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.ExtraFlightNotApproved, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsSuccessWhenBaselineMappingAllRequirementsMet()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.BaselineMapping);

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsSuccessWhenPlantHealthAllRequirementsMet()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth);

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
    }

    [Fact]
    public void EvaluateCommercialReadinessSucceedsWhenCommercialRequirementsMetEvenIfDroneNotReady()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth) with
        {
            IsDroneReady = false,
            IsPreflightCompleted = false,
            IsMissionPreparedForPurpose = false,
            IsMissionScheduledWithinAppointment = false,
            SafetyChecksSatisfied = false
        };

        var decision = SurveyOrderReadinessPolicy.EvaluateCommercialReadiness(snapshot);

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
    }

    [Fact]
    public void EvaluateOperationalReadinessFailsWhenDroneOrPreflightNotReady()
    {
        var commercialSnapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth);

        var decision = SurveyOrderReadinessPolicy.EvaluateOperationalReadiness(
            commercialSnapshot,
            isMissionPrepared: true,
            isMissionScheduled: true,
            isDroneReady: false,
            isPreflightCompleted: true,
            safetyChecksSatisfied: true);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.DroneNotReady, decision.Failures);
    }

    [Fact]
    public void EvaluateReturnsFailureWhenPaymentAmountOrCurrencyMismatched()
    {
        var snapshot = CreateValidSnapshot(SurveyOperationPurpose.PlantHealth) with
        {
            IsPaymentAmountAndCurrencyMatched = false
        };

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.PaymentAmountOrCurrencyMismatch, decision.Failures);
    }

    private static SurveyOrderReadinessSnapshot CreateValidSnapshot(SurveyOperationPurpose purpose)
    {
        var isBaseline = purpose == SurveyOperationPurpose.BaselineMapping;
        var orderStatus = isBaseline ? SurveyOrderStatus.BaselineReady : SurveyOrderStatus.ReadyForPaidService;

        return new SurveyOrderReadinessSnapshot(
            OrderStatus: orderStatus,
            Purpose: purpose,
            RequiresBaselineMapping: isBaseline,
            HasApprovedFarmBoundary: !isBaseline,
            IsScopeConfirmed: !isBaseline,
            HasPublishedFarmBaseMap: !isBaseline,
            HasConfirmedPoleCount: true,
            HasPriceSnapshot: true,
            AppointmentPurpose: isBaseline ? SurveyAppointmentPurpose.BaselineMapping : SurveyAppointmentPurpose.PaidService,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: isBaseline ? null : SurveyPaymentStatus.Paid,
            HasActiveQualifiedPrimaryManager: true,
            IsPrimaryManagerAvailable: true,
            IsAppointmentCurrent: true,
            IsMissionPreparedForPurpose: true,
            IsMissionScheduledWithinAppointment: true,
            IsDroneReady: true,
            IsPreflightCompleted: true,
            HasPendingPriceAdjustment: false,
            IsPaymentAmountAndCurrencyMatched: true,
            SafetyChecksSatisfied: true,
            HasValidWrittenDataUseConsent: true,
            HasValidFlightPermission: true,
            HasPendingFinancialSettlement: false,
            HasUnapprovedExtraFlight: false,
            HasFinalizedBoundaryCapture: false,
            HasApprovedBaselineRoute: false,
            IsBaselineRouteCurrent: false,
            IsBaselineRouteWithinSafetyInset: false,
            DepositPaymentStatus: isBaseline ? SurveyPaymentStatus.Paid : null);
    }
}
