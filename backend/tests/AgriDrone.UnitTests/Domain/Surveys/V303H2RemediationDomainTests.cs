using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class V303H2RemediationDomainTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DepositObligationReversalReopensSatisfiedFundingGate()
    {
        var obligation = SurveyDepositObligation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100000m,
            "VND",
            T0);
        obligation.ApplyAllocation(100000m, T0.AddMinutes(1));

        obligation.ReverseAllocation(40000m, T0.AddMinutes(2));

        Assert.False(obligation.IsSatisfied);
        Assert.Equal(60000m, obligation.AllocatedAmount);
        Assert.Equal(40000m, obligation.RemainingAmount);
        Assert.Equal(SurveyDepositObligationStatus.Pending, obligation.Status);
        Assert.Null(obligation.SatisfiedAt);
    }

    [Fact]
    public void FundingReversalCreatesImmutableProviderFact()
    {
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();

        var reversal = SurveyFundingReversal.Create(
            Guid.NewGuid(),
            paymentId,
            orderId,
            obligationId,
            "sepay",
            "chargeback-001",
            "payment.chargeback",
            100000m,
            "VND",
            T0,
            T0.AddSeconds(1));

        Assert.Equal(paymentId, reversal.SurveyPaymentId);
        Assert.Equal(orderId, reversal.SurveyOrderId);
        Assert.Equal(obligationId, reversal.DepositObligationId);
        Assert.Equal(100000m, reversal.Amount);
        Assert.Equal("payment.chargeback", reversal.EventType);
    }

    [Fact]
    public void AppointmentNoShowPinsOccurrenceDispositionAndIdempotencyIdentity()
    {
        var operationId = Guid.NewGuid();
        var noShow = SurveyAppointmentNoShow.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            SurveyNoShowDisposition.FreeReschedule,
            Guid.NewGuid(),
            "Owner did not attend",
            "OPS-EVIDENCE-001",
            operationId,
            "payload-fingerprint",
            T0,
            T0.AddSeconds(1));

        Assert.Equal(1, noShow.OccurrenceIndex);
        Assert.Equal(SurveyNoShowDisposition.FreeReschedule, noShow.Disposition);
        Assert.Equal(operationId, noShow.OperationId);
        Assert.Equal("payload-fingerprint", noShow.PayloadFingerprint);
    }
}
