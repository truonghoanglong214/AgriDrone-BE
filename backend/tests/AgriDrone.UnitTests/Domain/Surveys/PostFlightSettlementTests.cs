using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class PostFlightSettlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FirstAndFollowUpOnlyBillAfterFlightAndReview(bool first)
    {
        var order = Create(first);
        var boundary = Guid.NewGuid();
        var map = Guid.NewGuid();
        var manager = Guid.NewGuid();
        if (first)
        {
            order.MarkBaselineAppointmentConfirmed(Now);
            order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 5000m, Now);
            order.StartBaselineMapping(Now);
            order.MarkBaselineCandidatesReady(Now);
            order.RecordBaselinePublication(map, ConfirmedSurveyPoleCount.Create(20), manager, Now, boundary);
        }
        else
        {
            order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 5000m, Now);
            order.VerifyBoundaryScopeAndCurrentInventory(boundary, map, ConfirmedSurveyPoleCount.Create(20), manager, Now);
        }

        Assert.Throws<SurveyDomainException>(() => Confirm(order, map, manager));
        Assert.Null(order.FinalPrice);
        order.MarkPaidAppointmentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
        order.StartPaidService(Now);
        order.MarkAnalysisReady(Now);
        Assert.Throws<SurveyDomainException>(() => Confirm(order, map, manager));
        order.RecordReviewedServicePublication(Now);
        Confirm(order, map, manager);
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(0m, order.ComboDiscountPercentage);
        Assert.Equal(1000m, order.PricePerPoleSnapshot?.Amount);
        Assert.Equal(20000m, order.FinalPrice);
        Assert.Equal(15000m, order.RemainingAmount);
        Assert.Equal(order.BookedServicePriceId, order.SurveyServicePriceId);
        order.MarkPaymentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.Completed, order.Status);
        Assert.Throws<SurveyDomainException>(() => order.MarkPaymentConfirmed(Now));
    }

    [Fact]
    public void FollowUpAppointmentCannotAuthorizeFlightBeforeDeposit()
    {
        var order = Create(false);
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(20), Guid.NewGuid(), Now);
        order.MarkPaidAppointmentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineDeposit, order.Status);
        Assert.Throws<SurveyDomainException>(() => order.StartPaidService(Now));
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 5000m, Now);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
    }

    [Theory]
    [InlineData(SurveyPaymentStatus.Pending, false)]
    [InlineData(SurveyPaymentStatus.Paid, true)]
    public void FlightReadinessRequiresDepositButNotFinalBalance(SurveyPaymentStatus deposit, bool ready)
    {
        var state = new SurveyOrderReadinessSnapshot(SurveyOrderStatus.ReadyForPaidService,
            SurveyOperationPurpose.PlantHealth, false, true, true, true, false, false,
            SurveyAppointmentPurpose.PaidService, SurveyAppointmentStatus.Confirmed, null,
            true, false, true) with { PostFlightSettlement = true, DepositPaymentStatus = deposit };
        Assert.Equal(ready, SurveyOrderReadinessPolicy.EvaluateCommercialReadiness(state).IsReady);
    }

    [Fact]
    public void RegistrationV26DoesNotAllowWaivingThePreFlightDeposit()
    {
        var order = SurveyOrder.Create("NO-DEPOSIT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            false, null, "v2.6", "NEW_ORDER", Now, 0m, 0m);
        Assert.Throws<InvalidOperationException>(() => order.EnablePostFlightSettlement(
            SurveyServiceType.PlantHealth, Guid.NewGuid(), 1000m));
    }

    [Fact]
    public void ReschedulingRequiresConfirmationAgainAndRetainsDeposit()
    {
        var order = Create(false);
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 5000m, Now);
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(20), Guid.NewGuid(), Now);
        order.MarkPaidAppointmentConfirmed(Now);
        order.ReopenPaidAppointment(Now);
        Assert.Equal(SurveyOrderStatus.AwaitingPaidAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        Assert.Throws<SurveyDomainException>(() => order.StartPaidService(Now));
        order.MarkPaidAppointmentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
    }

    [Fact]
    public void CommercialSnapshotCannotBeReplacedOrCompletedBeforeReview()
    {
        var order = Create(false);
        Assert.Throws<InvalidOperationException>(() => order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 9000m));
        Assert.Throws<SurveyDomainException>(() => order.MarkPaymentConfirmed(Now));
        Assert.Throws<SurveyDomainException>(() => order.Complete(Now));
        Assert.Equal(1000m, order.BookedPricePerPole);
    }

    private static SurveyOrder Create(bool first)
    {
        var order = SurveyOrder.Create("POST-FLIGHT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            first, null, "v2.6", "NEW_ORDER", Now, 5000m, 20m);
        order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 1000m);
        return order;
    }

    private static void Confirm(SurveyOrder order, Guid map, Guid manager) =>
        order.ConfirmPoleCountAndPricing(map, ConfirmedSurveyPoleCount.Create(20),
            order.RequiresBaselineMapping ? SurveyPoleCountSource.BaselinePublication : SurveyPoleCountSource.CurrentPublishedInventory,
            "Reviewed post-flight poles", order.BookedServicePriceId!.Value,
            PricePerPole.Create(9999m), CurrencyCode.Vnd, manager, Now);
}
