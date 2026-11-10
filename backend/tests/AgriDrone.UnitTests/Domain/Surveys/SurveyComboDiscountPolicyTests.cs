using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyComboDiscountPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void CreateWithValidParametersSetsPropertiesCorrectly()
    {
        // Arrange & Act
        var policy = SurveyComboDiscountPolicy.Create(
            versionNumber: 1,
            discountPercentage: 20.00m,
            createdBy: AdminId,
            effectiveFrom: Now);

        // Assert
        Assert.NotEqual(Guid.Empty, policy.Id);
        Assert.Equal(1, policy.VersionNumber);
        Assert.Equal(20.00m, policy.DiscountPercentage);
        Assert.Equal(AdminId, policy.CreatedBy);
        Assert.Equal(Now, policy.EffectiveFrom);
        Assert.Null(policy.EffectiveTo);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CreateWithInvalidPercentageThrowsArgumentOutOfRangeException(decimal invalidPercentage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyComboDiscountPolicy.Create(1, invalidPercentage, AdminId, Now));
    }

    [Fact]
    public void CloseAtSetsEffectiveToWhenValid()
    {
        // Arrange
        var policy = SurveyComboDiscountPolicy.Create(1, 20.00m, AdminId, Now);
        var closeTime = Now.AddDays(7);

        // Act
        policy.CloseAt(closeTime);

        // Assert
        Assert.Equal(closeTime, policy.EffectiveTo);
    }

    [Fact]
    public void SurveyOrderCalculatesComboDiscountCorrectlyWhenRequiresBaselineMapping()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var order = SurveyOrder.Create(
            "ORD-COMBO-01",
            tenantId,
            farmId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping: true,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "New farm",
            createdAt: Now,
            customDepositAmount: 500_000m,
            customComboDiscountPercentage: 20.00m);

        Assert.Equal(20.00m, order.ComboDiscountPercentage);
        Assert.Equal(500_000m, order.DepositAmount);

        // Transition through baseline states to AwaitingPricing
        order.MarkBaselineAppointmentConfirmed(Now.AddHours(1));
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), order.DepositAmount, Now.AddHours(2));
        order.StartBaselineMapping(Now.AddHours(4));
        order.MarkBaselineCandidatesReady(Now.AddHours(5));

        var mapVersionId = Guid.NewGuid();
        var boundaryVersionId = Guid.NewGuid();
        order.RecordBaselinePublication(
            farmBaseMapVersionId: mapVersionId,
            confirmedPoleCount: ConfirmedSurveyPoleCount.Create(1000),
            confirmedBy: AdminId,
            confirmedAt: Now.AddHours(6),
            farmBoundaryVersionId: boundaryVersionId);

        // Act: Confirm pricing with 1000 poles at 2,500 VND catalog price
        var priceVersionId = Guid.NewGuid();
        order.ConfirmPoleCountAndPricing(
            farmBaseMapVersionId: mapVersionId,
            confirmedPoleCount: ConfirmedSurveyPoleCount.Create(1000),
            poleCountSource: SurveyPoleCountSource.BaselinePublication,
            evidenceReference: "Orthophoto inspection",
            surveyServicePriceId: priceVersionId,
            pricePerPole: PricePerPole.Create(2_500m),
            currency: CurrencyCode.Vnd,
            confirmedBy: AdminId,
            confirmedAt: Now.AddHours(7));

        // Assert:
        // Original snapshot preserves the catalog undiscounted price: 1000 * 2,500 = 2,500,000
        Assert.Equal(2_500m, order.OriginalPricePerPoleSnapshot?.Amount);
        Assert.Equal(2_500_000m, order.OriginalFinalPrice);

        // Discounted price per pole: 2,500 * (1 - 0.20) = 2,000 VND
        Assert.Equal(2_000m, order.PricePerPoleSnapshot?.Amount);
        // FinalPrice: 1000 * 2,000 = 2,000,000 VND
        Assert.Equal(2_000_000m, order.FinalPrice);

        // RemainingAmount: 2,000,000 - 500,000 (deposit credit) = 1,500,000 VND
        Assert.Equal(1_500_000m, order.RemainingAmount);
    }
}
