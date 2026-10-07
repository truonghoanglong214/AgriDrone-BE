using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyPoliciesTests
{
    [Fact]
    public void PaidReadinessRequiresCommercialAndOperationalPrerequisites()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.AwaitingPayment,
            Purpose: SurveyOperationPurpose.PlantHealth,
            RequiresBaselineMapping: false,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: false,
            HasPriceSnapshot: false,
            AppointmentPurpose: SurveyAppointmentPurpose.PaidService,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: SurveyPaymentStatus.Pending,
            HasActiveQualifiedPrimaryManager: false,
            HasPendingPriceAdjustment: true,
            SafetyChecksSatisfied: false));

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.OrderNotEligible, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.FarmBaseMapNotPublished, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PoleCountNotConfirmed, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PriceNotConfirmed, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PaymentNotConfirmed, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PrimaryManagerNotReady, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PriceAdjustmentPending, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.SafetyChecksNotSatisfied, decision.Failures);
    }

    [Fact]
    public void BaselineReadinessDoesNotRequirePriceOrPayment()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.BaselineReady,
            Purpose: SurveyOperationPurpose.BaselineMapping,
            RequiresBaselineMapping: true,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: false,
            HasPriceSnapshot: false,
            AppointmentPurpose: SurveyAppointmentPurpose.BaselineMapping,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: null,
            HasActiveQualifiedPrimaryManager: true,
            HasPendingPriceAdjustment: false,
            SafetyChecksSatisfied: true));

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
    }

    [Theory]
    [InlineData(SurveyOperationPurpose.PlantHealth)]
    [InlineData(SurveyOperationPurpose.HarvestReadiness)]
    public void PaidReadinessAllowsOrderWhenEveryPrerequisiteIsSatisfied(
        SurveyOperationPurpose purpose)
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.ReadyForPaidService,
            Purpose: purpose,
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
            HasPendingPriceAdjustment: false,
            SafetyChecksSatisfied: true));

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
    }

    [Fact]
    public void BaselineReadinessRejectsMappedFarm()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.BaselineReady,
            Purpose: SurveyOperationPurpose.BaselineMapping,
            RequiresBaselineMapping: false,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: false,
            HasConfirmedPoleCount: false,
            HasPriceSnapshot: false,
            AppointmentPurpose: SurveyAppointmentPurpose.BaselineMapping,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: null,
            HasActiveQualifiedPrimaryManager: true,
            HasPendingPriceAdjustment: false,
            SafetyChecksSatisfied: true));

        Assert.False(decision.IsReady);
        Assert.Contains(
            SurveyOrderReadinessFailure.BaselineMappingNotRequired,
            decision.Failures);
    }

    [Fact]
    public void ReadinessRejectsAppointmentForAnotherPurpose()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            OrderStatus: SurveyOrderStatus.ReadyForPaidService,
            Purpose: SurveyOperationPurpose.PlantHealth,
            RequiresBaselineMapping: false,
            HasApprovedFarmBoundary: true,
            IsScopeConfirmed: true,
            HasPublishedFarmBaseMap: true,
            HasConfirmedPoleCount: true,
            HasPriceSnapshot: true,
            AppointmentPurpose: SurveyAppointmentPurpose.BaselineMapping,
            AppointmentStatus: SurveyAppointmentStatus.Confirmed,
            PaymentStatus: SurveyPaymentStatus.Confirmed,
            HasActiveQualifiedPrimaryManager: true,
            HasPendingPriceAdjustment: false,
            SafetyChecksSatisfied: true));

        Assert.False(decision.IsReady);
        Assert.Contains(
            SurveyOrderReadinessFailure.AppointmentPurposeMismatch,
            decision.Failures);
    }

    [Theory]
    [InlineData("VND", "VND")]
    [InlineData(" usd ", "USD")]
    public void CurrencyUsesNormalizedIsoCode(string input, string expected)
    {
        Assert.Equal(expected, CurrencyCode.Create(input).Value);
    }

    [Fact]
    public void SurveyPriceUsesAdrRoundingRule()
    {
        var unitPrice = PricePerPole.Create(10.005m);
        var poleCount = ConfirmedSurveyPoleCount.Create(3);
        var total = Money.CalculateSurveyPrice(
            poleCount,
            unitPrice,
            CurrencyCode.Vnd);

        Assert.Equal(10.01m, unitPrice.Amount);
        Assert.Equal(30.03m, total.Amount);
        Assert.Equal(CurrencyCode.Vnd, total.Currency);
    }

    [Fact]
    public void IdempotencyIsScopedAndNormalized()
    {
        var value = RequestIdempotency.Create(" Public:Email ", " request-01 ");

        Assert.Equal("public:email", value.CallerScope);
        Assert.Equal("request-01", value.Key);
    }

    [Fact]
    public void PaymentEventIdentityUsesProviderReferenceAndEventId()
    {
        var first = PaymentEventIdentity.Create(" Provider ", "PAY-01", "evt-01");
        var duplicate = PaymentEventIdentity.Create("provider", "PAY-01", "evt-01");
        var nextEvent = PaymentEventIdentity.Create("provider", "PAY-01", "evt-02");

        Assert.Equal(first.DeduplicationKey, duplicate.DeduplicationKey);
        Assert.NotEqual(first.DeduplicationKey, nextEvent.DeduplicationKey);
        Assert.Equal(64, first.DeduplicationKey.Length);
    }
}
