using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class PostFlightSettlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void ComboAndFollowUpComboApplyFrozenDiscountOnlyAfterFlightAndReview(bool first, bool separatePricing)
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

        void ConfirmFinal()
        {
            if (separatePricing)
                order.ConfirmPricing(Guid.NewGuid(), PricePerPole.Create(9999m), CurrencyCode.Vnd, manager, Now);
            else
                Confirm(order, map, manager);
        }

        Assert.Throws<SurveyDomainException>(ConfirmFinal);
        Assert.Null(order.FinalPrice);
        order.MarkPaidAppointmentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.ReadyForPaidService, order.Status);
        order.StartPaidService(Now);
        order.MarkAnalysisReady(Now);
        Assert.Throws<SurveyDomainException>(ConfirmFinal);
        order.RecordReviewedServicePublication(Now);
        ConfirmFinal();
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(20m, order.ComboDiscountPercentage);
        Assert.Equal(800m, order.PricePerPoleSnapshot?.Amount);
        Assert.Equal(1000m, order.BookedPricePerPole);
        Assert.Equal(20000m, order.OriginalFinalPrice);
        Assert.Equal(order.BookedServicePriceId, order.OriginalSurveyServicePriceId);
        Assert.Equal(16000m, order.FinalPrice);
        Assert.Equal(11000m, order.RemainingAmount);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscountUsesFrozenBookingRateOnceDespiteDifferentCatalogueOrAlreadyDiscountedInput(bool first)
    {
        var order = SurveyOrder.Create("PINNED-DISCOUNT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            first, null, "v2.6", "NEW_ORDER", Now, 5000m, 12.5m);
        order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 1001m);

        var effective = order.GetEffectivePricePerPole(PricePerPole.Create(9999m));
        Assert.Equal(876m, effective.Amount);
        Assert.Equal(876m, order.GetEffectivePricePerPole(effective).Amount);
        Assert.Equal(12.5m, order.ComboDiscountPercentage);
        Assert.Equal(1001m, order.BookedPricePerPole);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComponentComboBillsAllMappedPolesAndOnlyHealthScopeAtDifferentRates(bool first)
    {
        var order = SurveyOrder.Create("COMPONENT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            first, null, "components.v1", "NEW_ORDER", Now, 500000m, 20m);
        var priceId = Guid.NewGuid();
        order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, priceId, 2500m, 1500m, 1000m);
        var map = Guid.NewGuid(); var boundary = Guid.NewGuid(); var manager = Guid.NewGuid();
        if (first)
        {
            order.MarkBaselineAppointmentConfirmed(Now);
            order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 500000m, Now);
            order.StartBaselineMapping(Now); order.MarkBaselineCandidatesReady(Now);
            order.RecordBaselinePublication(map, ConfirmedSurveyPoleCount.Create(1000), manager, Now, boundary);
        }
        else
        {
            order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 500000m, Now);
            order.VerifyBoundaryScopeAndCurrentInventory(boundary, map, ConfirmedSurveyPoleCount.Create(1000), manager, Now);
        }
        var source = first ? SurveyPoleCountSource.BaselinePublication : SurveyPoleCountSource.CurrentPublishedInventory;
        Assert.Throws<SurveyDomainException>(() => order.ConfirmComboComponentPricing(map, 1000, 300, source, "review", manager, Now));
        order.MarkPaidAppointmentConfirmed(Now); order.StartPaidService(Now); order.MarkAnalysisReady(Now);
        Assert.Throws<SurveyDomainException>(() => order.ConfirmComboComponentPricing(map, 1000, 300, source, "review", manager, Now));
        order.RecordReviewedServicePublication(Now);
        Assert.Throws<SurveyDomainException>(() => order.ConfirmPricing(priceId, PricePerPole.Create(9999m), CurrencyCode.Vnd, manager, Now));
        Assert.Throws<SurveyDomainException>(() => order.ConfirmPoleCountAndPricing(map, ConfirmedSurveyPoleCount.Create(300), source,
            "review", priceId, PricePerPole.Create(9999m), CurrencyCode.Vnd, manager, Now));
        Assert.Throws<SurveyDomainException>(() => order.ConfirmComboComponentPricing(map, 1000, 1001, source, "review", manager, Now));
        Assert.Null(order.FinalPrice);

        order.ConfirmComboComponentPricing(map, 1000, 300, source, "reviewed full map and selected Health scope", manager, Now);

        Assert.Equal(1000, order.ConfirmedMappingPoleCount);
        Assert.Equal(300, order.ConfirmedPlantHealthPoleCount);
        Assert.Equal(1500000m, order.MappingSubtotal);
        Assert.Equal(300000m, order.PlantHealthSubtotal);
        Assert.Equal(360000m, order.ComboDiscountAmount);
        Assert.Equal(1800000m, order.OriginalFinalPrice);
        Assert.Equal(1440000m, order.FinalPrice);
        Assert.Equal(940000m, order.RemainingAmount);
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(1500m, order.BookedMappingPricePerPole);
        Assert.Equal(1000m, order.BookedPlantHealthPricePerPole);
        Assert.Throws<SurveyDomainException>(() => order.ApplyPriceAdjustment(priceId, ConfirmedSurveyPoleCount.Create(300),
            PricePerPole.Create(1m), CurrencyCode.Vnd, manager, Now));
        Assert.Equal(1440000m, order.FinalPrice);
        order.MarkPaymentConfirmed(Now);
        Assert.Equal(SurveyOrderStatus.Completed, order.Status);
    }

    [Theory]
    [InlineData(1500, 1000, 2500, true)]
    [InlineData(1500, 1000, 2600, false)]
    [InlineData(0, 1000, 1000, false)]
    public void CatalogueComponentRatesMustBePositiveAndSumToCombinedNominalRate(decimal mapRate, decimal healthRate, decimal combined, bool valid)
    {
        SurveyServicePrice CreatePrice() => SurveyServicePrice.CreatePerPole(Guid.NewGuid(), PricePerPole.Create(combined),
            CurrencyCode.Vnd, Now, null, Guid.NewGuid(), Now, mapRate, healthRate);
        if (!valid) Assert.Throws<ArgumentException>(() => CreatePrice());
        else
        {
            var price = CreatePrice();
            Assert.Equal(mapRate, price.MappingPricePerPole);
            Assert.Equal(healthRate, price.PlantHealthPricePerPole);
            Assert.Equal(combined, price.PricePerPole!.Value.Amount);
        }
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
