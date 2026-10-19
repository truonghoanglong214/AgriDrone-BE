using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class R2AdjustmentTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ScopeAdjustmentLifecycleTransitionsCorrectly()
    {
        var orderId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var adjustment = ScopeAdjustment.Create(
            orderId,
            ScopeAdjustmentReason.ConfirmedPoleCountChanged,
            null,
            null,
            ConfirmedSurveyPoleCount.Create(100),
            ConfirmedSurveyPoleCount.Create(120),
            100000m,
            CurrencyCode.Create("VND"),
            "doc-evidence-1",
            requesterId,
            Now);

        Assert.Equal(ScopeAdjustmentStatus.Pending, adjustment.Status);
        Assert.Equal(0u, adjustment.Version);

        // Version mismatch throws
        Assert.Throws<InvalidOperationException>(() =>
            adjustment.Review(true, "Looks good", reviewerId, Now.AddMinutes(5), 99));

        // Review approve
        adjustment.Review(true, "Looks good", reviewerId, Now.AddMinutes(5), 0);
        Assert.Equal(ScopeAdjustmentStatus.Approved, adjustment.Status);
        Assert.Equal(1u, adjustment.Version);
        Assert.Equal(reviewerId, adjustment.ReviewedBy);

        // Mark applied
        adjustment.MarkApplied(Now.AddMinutes(10), 1);
        Assert.Equal(ScopeAdjustmentStatus.Applied, adjustment.Status);
        Assert.Equal(2u, adjustment.Version);
    }

    [Fact]
    public void ScopeAdjustmentNoChangeThrowsArgumentException()
    {
        var orderId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            ScopeAdjustment.Create(
                orderId,
                ScopeAdjustmentReason.ConfirmedPoleCountChanged,
                boundaryId,
                boundaryId,
                ConfirmedSurveyPoleCount.Create(100),
                ConfirmedSurveyPoleCount.Create(100),
                0m,
                CurrencyCode.Create("VND"),
                "no-change",
                Guid.NewGuid(),
                Now));
    }

    [Fact]
    public void AdjustmentSettlementLifecycleTransitionsCorrectly()
    {
        var adjustmentId = Guid.NewGuid();

        var settlement = AdjustmentSettlement.Create(
            adjustmentId,
            AdjustmentSettlementType.AdditionalPayment,
            500000m,
            CurrencyCode.Create("VND"),
            "idem-key-1",
            Now);

        Assert.Equal(AdjustmentSettlementStatus.Pending, settlement.Status);
        Assert.Equal(500000m, settlement.Amount);
        Assert.Equal(0u, settlement.Version);

        // Mark processing
        settlement.MarkProcessing("pay-prov-ref-1", Now.AddMinutes(1), 0);
        Assert.Equal(AdjustmentSettlementStatus.Processing, settlement.Status);
        Assert.Equal("pay-prov-ref-1", settlement.ProviderReference);
        Assert.Equal(1u, settlement.Version);

        // Confirm
        settlement.Confirm(null, Now.AddMinutes(2), 1);
        Assert.Equal(AdjustmentSettlementStatus.Confirmed, settlement.Status);
        Assert.Equal(2u, settlement.Version);
    }

    [Fact]
    public void AdjustmentSettlementFailRecordsFailureReason()
    {
        var adjustmentId = Guid.NewGuid();

        var settlement = AdjustmentSettlement.Create(
            adjustmentId,
            AdjustmentSettlementType.Refund,
            200000m,
            CurrencyCode.Create("VND"),
            "idem-key-2",
            Now);

        settlement.Fail("Bank account closed", Now.AddMinutes(1), 0);
        Assert.Equal(AdjustmentSettlementStatus.Failed, settlement.Status);
        Assert.Equal("Bank account closed", settlement.FailureReason);
        Assert.Equal(1u, settlement.Version);
    }

    [Fact]
    public void ExtraFlightRequestLifecycleTransitionsCorrectly()
    {
        var orderId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        var request = ExtraFlightRequest.Create(
            orderId,
            farmId,
            null,
            ExtraFlightReason.ApprovedScopeChange,
            "evidence-flight",
            150000m,
            CurrencyCode.Create("VND"),
            requesterId,
            Now);

        Assert.Equal(ExtraFlightRequestStatus.Pending, request.Status);
        Assert.Equal(0u, request.Version);

        // Review approve
        request.Review(true, reviewerId, Now.AddMinutes(5), 0);
        Assert.Equal(ExtraFlightRequestStatus.Approved, request.Status);
        Assert.Equal(1u, request.Version);

        // Consume
        request.Consume(missionId, Now.AddMinutes(10), 1);
        Assert.Equal(ExtraFlightRequestStatus.Consumed, request.Status);
        Assert.Equal(missionId, request.ExtraMissionId);
        Assert.Equal(2u, request.Version);
    }

    [Fact]
    public void ReadinessPolicyEnforcesAuthorizationsAndAdjustments()
    {
        var snapshot = new SurveyOrderReadinessSnapshot(
            OrderStatus: SurveyOrderStatus.ReadyForPaidService,
            Purpose: SurveyOperationPurpose.PlantHealth,
            RequiresBaselineMapping: false,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: true,
            HasConfirmedPoleCount: true,
            HasPriceSnapshot: true,
            AppointmentPurpose: SurveyAppointmentPurpose.PaidService,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: SurveyPaymentStatus.Confirmed,
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
            HasValidWrittenDataUseConsent: false, // missing consent
            HasValidFlightPermission: true,
            HasPendingFinancialSettlement: true,  // pending settlement
            HasUnapprovedExtraFlight: true);      // unapproved extra flight

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.WrittenConsentNotValid, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.FinancialSettlementPending, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.ExtraFlightNotApproved, decision.Failures);
    }
}
