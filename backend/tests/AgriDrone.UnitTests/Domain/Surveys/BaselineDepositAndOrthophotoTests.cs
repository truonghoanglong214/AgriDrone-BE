using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class BaselineDepositAndOrthophotoTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnmappedOrderStartsInAwaitingBaselineAppointmentWithPendingDeposit()
    {
        var order = CreateOrder(requiresBaselineMapping: true);

        Assert.True(order.RequiresBaselineMapping);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
        Assert.Equal(500_000m, order.DepositAmount);
        Assert.Null(order.RemainingAmount);
        Assert.Null(order.DepositPaymentId);
        Assert.Null(order.DepositConfirmedAt);
    }

    [Fact]
    public void ConfirmingBaselineAppointmentTransitionsToAwaitingBaselineDeposit()
    {
        var order = CreateOrder(requiresBaselineMapping: true);

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));

        Assert.Equal(SurveyOrderStatus.AwaitingBaselineDeposit, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
    }

    [Fact]
    public void ConfirmingBaselineDepositTransitionsToBaselineReady()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();
        var confirmedAt = Now.AddMinutes(5);

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, confirmedAt);

        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        Assert.Equal(paymentId, order.DepositPaymentId);
        Assert.Equal(confirmedAt, order.DepositConfirmedAt);
    }

    [Fact]
    public void RecordingWrongDepositAmountThrowsDomainException()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));

        var ex = Assert.Throws<SurveyDomainException>(() =>
            order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 250_000m, Now.AddMinutes(5)));

        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void SetFarmBoundaryFromOrthophotoInAwaitingBaselineReview()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, order.Status);

        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, order.Status);

        order.SetFarmBoundaryFromOrthophoto(boundaryId, managerId, Now.AddHours(3));

        Assert.Equal(boundaryId, order.FarmBoundaryVersionId);
        Assert.Equal(managerId, order.ScopeConfirmedBy);
        Assert.Equal(Now.AddHours(3), order.ScopeConfirmedAt);
    }

    [Fact]
    public void PublishingBaselineAndConfirmingPricingCalculatesRemainingAmountMinusDeposit()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var baseMapId = Guid.NewGuid();
        var priceId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(boundaryId, managerId, Now.AddHours(3));

        // 120 poles confirmed
        var poleCount = ConfirmedSurveyPoleCount.Create(120);
        order.RecordBaselinePublication(baseMapId, poleCount, managerId, Now.AddHours(4));
        Assert.Equal(SurveyOrderStatus.AwaitingPricing, order.Status);

        // Price: 25,000 VND / pole => FinalPrice = 120 * 25,000 = 3,000,000 VND
        // Deposit credit: 500,000 VND => RemainingAmount = 2,500,000 VND
        order.ConfirmPricing(
            priceId,
            PricePerPole.Create(25_000m),
            CurrencyCode.Vnd,
            managerId,
            Now.AddHours(5));

        Assert.Equal(SurveyOrderStatus.AwaitingPaidAppointment, order.Status);
        Assert.Equal(3_000_000m, order.FinalPrice);
        Assert.Equal(2_500_000m, order.RemainingAmount);
    }

    [Fact]
    public void CompletingPaidFlowWithRemainingBalance()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var managerId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(Guid.NewGuid(), managerId, Now.AddHours(3));
        order.RecordBaselinePublication(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(80), managerId, Now.AddHours(4));

        // FinalPrice = 80 * 20,000 = 1,600,000 VND; RemainingAmount = 1,100,000 VND
        order.ConfirmPricing(Guid.NewGuid(), PricePerPole.Create(20_000m), CurrencyCode.Vnd, managerId, Now.AddHours(5));
        Assert.Equal(1_100_000m, order.RemainingAmount);

        order.MarkPaidAppointmentConfirmed(Now.AddHours(6));
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);

        order.MarkPaymentConfirmed(Now.AddHours(7));
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);

        order.StartPaidService(Now.AddHours(8));
        Assert.Equal(SurveyOrderStatus.InProgress, order.Status);

        order.MarkAnalysisReady(Now.AddHours(9));
        Assert.Equal(SurveyOrderStatus.PendingReview, order.Status);

        order.Complete(Now.AddHours(10));
        Assert.Equal(SurveyOrderStatus.Completed, order.Status);
    }

    [Fact]
    public void AlreadyMappedFarmDoesNotRequireBaselineDeposit()
    {
        var order = CreateOrder(requiresBaselineMapping: false);

        Assert.False(order.RequiresBaselineMapping);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, order.Status);
        Assert.Equal(0m, order.DepositAmount);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
    }

    [Fact]
    public void PaymentPurposeEnumDistinguishesBaselineDepositAndFinalBalance()
    {
        var depositPayment = SurveyPayment.Create(
            Guid.NewGuid(),
            Money.Create(500_000m, CurrencyCode.Vnd),
            "SANDBOX",
            Now,
            PaymentPurpose.BaselineDeposit);

        var finalPayment = SurveyPayment.Create(
            Guid.NewGuid(),
            Money.Create(2_500_000m, CurrencyCode.Vnd),
            "SANDBOX",
            Now,
            PaymentPurpose.FinalBalance);

        Assert.Equal(PaymentPurpose.BaselineDeposit, depositPayment.Purpose);
        Assert.Equal(PaymentPurpose.FinalBalance, finalPayment.Purpose);
    }

    [Fact]
    public void ReadinessPolicyFailsWhenBaselineDepositIsPending()
    {
        var snapshot = new SurveyOrderReadinessSnapshot(
            OrderStatus: SurveyOrderStatus.BaselineReady,
            Purpose: SurveyOperationPurpose.BaselineMapping,
            RequiresBaselineMapping: true,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: true,
            HasPriceSnapshot: true,
            AppointmentPurpose: SurveyAppointmentPurpose.BaselineMapping,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: null,
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
            HasPendingFinancialSettlement: false,
            HasUnapprovedExtraFlight: false,
            HasFinalizedBoundaryCapture: true,
            HasApprovedBaselineRoute: true,
            IsBaselineRouteCurrent: true,
            IsBaselineRouteWithinSafetyInset: true,
            DepositPaymentStatus: SurveyPaymentStatus.Pending); // Pending deposit

        var decision = SurveyOrderReadinessPolicy.Evaluate(snapshot);

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.PaymentNotConfirmed, decision.Failures);
    }

    [Fact]
    public void SetFarmBoundaryFromOrthophotoInBaselineReadyOrInProgressThrowsDomainException()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);

        // Cannot mark boundary in BaselineReady
        var ex1 = Assert.Throws<SurveyDomainException>(() =>
            order.SetFarmBoundaryFromOrthophoto(boundaryId, managerId, Now.AddMinutes(10)));
        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, ex1.Code);

        // Start flight
        order.StartBaselineMapping(Now.AddHours(1));
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, order.Status);

        // Cannot mark boundary in BaselineInProgress
        var ex2 = Assert.Throws<SurveyDomainException>(() =>
            order.SetFarmBoundaryFromOrthophoto(boundaryId, managerId, Now.AddHours(1).AddMinutes(30)));
        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, ex2.Code);

        // But once candidates / orthophoto is ready -> AwaitingBaselineReview -> ALLOWED
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, order.Status);
        order.SetFarmBoundaryFromOrthophoto(boundaryId, managerId, Now.AddHours(3));
        Assert.Equal(boundaryId, order.FarmBoundaryVersionId);
    }

    [Fact]
    public void RescheduleFromAwaitingBaselineDepositTransitionsToAwaitingBaselineAppointment()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineDeposit, order.Status);

        order.ReopenBaselineAppointment(Now.AddMinutes(10));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
    }

    [Fact]
    public void RescheduleFromBaselineReadyPreservesConfirmedDepositAndConfirmingNewAppointmentTransitionsBackToBaselineReady()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        // Initial appointment & deposit
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);

        // Reschedule
        order.ReopenBaselineAppointment(Now.AddMinutes(30));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus); // Deposit remains paid!

        // Confirm new appointment -> directly transitions to BaselineReady!
        order.MarkBaselineAppointmentConfirmed(Now.AddHours(1));
        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);
    }

    [Fact]
    public void RevokingBaselineDepositRefundBeforeFlightTransitionsToAwaitingBaselineDeposit()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);

        // Refund deposit before flight
        order.RevokeBaselineDeposit(Now.AddMinutes(15));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineDeposit, order.Status);
        Assert.Equal(SurveyPaymentStatus.Refunded, order.DepositPaymentStatus);
        Assert.Null(order.DepositPaymentId);
        Assert.Null(order.DepositConfirmedAt);
    }

    [Fact]
    public void RevokingBaselineDepositRefundAfterFlightRemovesDepositCreditFromRemainingAmount()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var priceId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(Guid.NewGuid(), managerId, Now.AddHours(3));
        order.RecordBaselinePublication(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(100), managerId, Now.AddHours(4));

        // 100 poles * 20,000 = 2,000,000 VND; Deposit credit: 500,000 VND => Remaining: 1,500,000 VND
        order.ConfirmPricing(priceId, PricePerPole.Create(20_000m), CurrencyCode.Vnd, managerId, Now.AddHours(5));
        Assert.Equal(1_500_000m, order.RemainingAmount);

        // Post-flight chargeback/refund of deposit
        order.RevokeBaselineDeposit(Now.AddHours(6));
        Assert.Equal(SurveyPaymentStatus.Refunded, order.DepositPaymentStatus);
        // Deposit credit is now removed: RemainingAmount must be full FinalPrice (2,000,000 VND)
        Assert.Equal(2_000_000m, order.RemainingAmount);
    }

    private static SurveyOrder CreateOrder(bool requiresBaselineMapping) =>
        SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "test.v1",
            previousOrderCompatibilityReason: "NO_PREVIOUS_PUBLISHED_ORDER",
            createdAt: Now);
}
