using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class BoundaryCaptureTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AppointmentConfirmationDoesNotOpenBaselineReadiness()
    {
        var order = CreateOrder();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));

        Assert.Equal(SurveyOrderStatus.AwaitingBaselineDeposit, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
    }

    [Fact]
    public void RouteMustMatchFinalizedCaptureAndBoundary()
    {
        var order = CreateOrder();
        var captureId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBoundaryCapture(
            captureId,
            boundaryId,
            Guid.NewGuid(),
            Now.AddMinutes(2));

        Assert.Throws<SurveyDomainException>(() =>
            order.ApproveBaselineRoute(
                Guid.NewGuid(),
                Guid.NewGuid(),
                boundaryId,
                Now.AddMinutes(3)));

        order.ApproveBaselineRoute(
            Guid.NewGuid(),
            captureId,
            boundaryId,
            Now.AddMinutes(3));

        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);
        Assert.NotNull(order.BaselineRoutePlanId);
    }

    [Fact]
    public void SampleSyncIsIdempotentByClientOperationId()
    {
        var session = BoundaryCaptureSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);
        var operationId = Guid.NewGuid();

        var first = session.AddSample(
            operationId,
            0,
            10.1m,
            106.1m,
            0.5m,
            Now,
            Now.AddSeconds(1));
        var retry = session.AddSample(
            operationId,
            0,
            10.1m,
            106.1m,
            0.5m,
            Now,
            Now.AddSeconds(2));

        Assert.Same(first, retry);
        Assert.Single(session.Samples);
        Assert.Equal(1u, session.Version);
        Assert.Throws<InvalidOperationException>(() => session.AddSample(
            operationId,
            0,
            10.2m,
            106.1m,
            0.5m,
            Now,
            Now.AddSeconds(3)));
    }

    [Fact]
    public void FinalizedCaptureIsImmutable()
    {
        var session = BoundaryCaptureSession.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);
        AddSample(session, 0, 10.0m, 106.0m);
        AddSample(session, 1, 10.1m, 106.0m);
        AddSample(session, 2, 10.1m, 106.1m);
        var expectedVersion = session.Version;

        session.Finalize(
            Guid.NewGuid(),
            "evidence://capture/1",
            Guid.NewGuid(),
            Now.AddMinutes(1),
            expectedVersion);

        Assert.Equal(BoundaryCaptureSessionStatus.Finalized, session.Status);
        Assert.Throws<InvalidOperationException>(() =>
            AddSample(session, 3, 10.0m, 106.1m));
    }

    [Fact]
    public void BaselineReadinessRequiresBaselineReadyAndConfirmedDeposit()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.AwaitingBaselineDeposit,
            Purpose: SurveyOperationPurpose.BaselineMapping,
            RequiresBaselineMapping: true,
            HasApprovedFarmBoundary: false,
            IsScopeConfirmed: false,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: false,
            HasPriceSnapshot: false,
            AppointmentPurpose: SurveyAppointmentPurpose.BaselineMapping,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: null,
            HasActiveQualifiedPrimaryManager: true,
            IsPrimaryManagerAvailable: true,
            IsAppointmentCurrent: true,
            IsMissionPreparedForPurpose: false,
            IsMissionScheduledWithinAppointment: false,
            IsDroneReady: true,
            IsPreflightCompleted: false,
            HasPendingPriceAdjustment: false,
            IsPaymentAmountAndCurrencyMatched: true,
            SafetyChecksSatisfied: false,
            DepositPaymentStatus: SurveyPaymentStatus.Pending));

        Assert.Contains(
            SurveyOrderReadinessFailure.OrderNotEligible,
            decision.Failures);
        Assert.Contains(
            SurveyOrderReadinessFailure.PaymentNotConfirmed,
            decision.Failures);
    }

    private static void AddSample(
        BoundaryCaptureSession session,
        int sequence,
        decimal latitude,
        decimal longitude) => session.AddSample(
            Guid.NewGuid(),
            sequence,
            latitude,
            longitude,
            0.5m,
            Now,
            Now.AddSeconds(1));

    private static SurveyOrder CreateOrder() => SurveyOrder.Create(
        "SO-STEP4-001",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        requiresBaselineMapping: true,
        previousCompatibleOrderId: null,
        previousOrderCompatibilityPolicyVersion: "test.v1",
        previousOrderCompatibilityReason: "NO_COMPATIBLE_ORDER",
        createdAt: Now);
}
