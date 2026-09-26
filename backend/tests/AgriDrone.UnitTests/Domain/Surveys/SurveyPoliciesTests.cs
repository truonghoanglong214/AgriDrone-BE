using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyPoliciesTests
{
    [Fact]
    public void ReadinessRequiresServerOwnedOperationalPrerequisites()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            SurveyOrderStatus.AwaitingPayment,
            IsScopeConfirmed: true,
            SurveyAppointmentStatus.Confirmed,
            SurveyPaymentStatus.Pending,
            HasActiveQualifiedPrimaryManager: false,
            HasPendingPriceAdjustment: true));

        Assert.False(decision.IsReady);
        Assert.Contains(SurveyOrderReadinessFailure.PaymentNotConfirmed, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PrimaryManagerNotReady, decision.Failures);
        Assert.Contains(SurveyOrderReadinessFailure.PriceAdjustmentPending, decision.Failures);
    }

    [Fact]
    public void ReadinessAllowsOrderWhenEveryPrerequisiteIsSatisfied()
    {
        var decision = SurveyOrderReadinessPolicy.Evaluate(new(
            SurveyOrderStatus.ReadyForOperations,
            IsScopeConfirmed: true,
            SurveyAppointmentStatus.Confirmed,
            SurveyPaymentStatus.Confirmed,
            HasActiveQualifiedPrimaryManager: true,
            HasPendingPriceAdjustment: false));

        Assert.True(decision.IsReady);
        Assert.Empty(decision.Failures);
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
        var unitPrice = Money.Create(10.005m, CurrencyCode.Vnd);
        var total = Money.CalculateSurveyPrice(1.5m, unitPrice);

        Assert.Equal(10.01m, unitPrice.Amount);
        Assert.Equal(15.02m, total.Amount);
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
