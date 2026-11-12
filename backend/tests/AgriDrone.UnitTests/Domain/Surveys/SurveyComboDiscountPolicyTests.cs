using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyComboDiscountPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Theory]
    [InlineData(true, 0, 1500, 1000, 1500000, 1000000)]
    [InlineData(true, 20, 2500, 1000, 2000000, 1500000)]
    [InlineData(false, 0, 1500, 1050, 1575000, 1075000)]
    [InlineData(false, 20, 2500, 1050, 2100000, 1600000)]
    public void PricingCreditsDepositForAllFourPaymentScenarios(
        bool baseline, decimal discount, decimal unitPrice, int poles,
        decimal total, decimal balance)
    {
        var order = CreatePricedOrder(baseline, discount, unitPrice, poles, 500000m);
        Assert.Equal(total, order.FinalPrice);
        Assert.Equal(balance, order.RemainingAmount);
        Assert.Equal(discount, order.ComboDiscountPercentage);
    }

    [Fact]
    public void DepositCoveredOrderOnlySettlesAfterExplicitSettlementAndRefundRevokesIt()
    {
        var order = CreatePricedOrder(false, 20m, 2500m, 2, 5000m);
        Assert.Equal(0m, order.RemainingAmount);
        Assert.False(order.IsZeroBalanceSettled());
        order.MarkPaidAppointmentConfirmed(Now.AddHours(8));
        order.MarkPaymentConfirmed(Now.AddHours(9));
        Assert.True(order.IsZeroBalanceSettled());
        order.RevokeBaselineDeposit(Now.AddHours(10));
        Assert.False(order.IsZeroBalanceSettled());
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(order.FinalPrice, order.RemainingAmount);
        Assert.True(order.CanAcceptDepositPayment());
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 5000m, Now.AddHours(11));
        Assert.Equal(0m, order.RemainingAmount);
        Assert.False(order.IsZeroBalanceSettled());
    }

    [Fact]
    public void ComboPriceUsesWholeVndAndPreservesDiscountWhenInventoryChanges()
    {
        var order = CreatePricedOrder(false, 20m, 2501m, 1000, 0m);
        Assert.Equal(2001m, order.PricePerPoleSnapshot?.Amount);
        var adjustedPrice = order.GetEffectivePricePerPole(PricePerPole.Create(2501m));
        order.ApplyPriceAdjustment(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(1100),
            adjustedPrice, CurrencyCode.Vnd, AdminId, Now.AddHours(8));
        Assert.Equal(2201100m, order.FinalPrice);
        Assert.Equal(20m, order.ComboDiscountPercentage);
        Assert.Equal(decimal.Truncate(order.FinalPrice!.Value), order.FinalPrice);
    }

    [Fact]
    public void RefundedBaselineDepositCanBeRepaidWithoutRepeatingMapping()
    {
        var order = CreatePricedOrder(true, 20m, 2500m, 1000, 500000m);
        order.MarkPaidAppointmentConfirmed(Now.AddHours(8));
        order.MarkPaymentConfirmed(Now.AddHours(9));
        order.RevokeBaselineDeposit(Now.AddHours(10));
        Assert.True(order.CanAcceptDepositPayment());
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 500000m, Now.AddHours(11));
        Assert.Equal(SurveyOrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(1500000m, order.RemainingAmount);
    }

    private static SurveyOrder CreatePricedOrder(
        bool baseline, decimal discount, decimal unitPrice, int poles, decimal deposit)
    {
        var order = SurveyOrder.Create("ORD-REGRESSION", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), baseline, null, "v1", "Regression", Now,
            customDepositAmount: deposit, customComboDiscountPercentage: discount);
        var mapId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var count = ConfirmedSurveyPoleCount.Create(poles);
        if (baseline)
        {
            order.MarkBaselineAppointmentConfirmed(Now.AddHours(1));
            if (deposit > 0m)
                order.RecordBaselineDepositConfirmed(Guid.NewGuid(), deposit, Now.AddHours(2));
            order.StartBaselineMapping(Now.AddHours(3));
            order.MarkBaselineCandidatesReady(Now.AddHours(4));
            order.RecordBaselinePublication(mapId, count, AdminId, Now.AddHours(5), boundaryId);
        }
        else
        {
            if (deposit > 0m)
                order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), deposit, Now.AddHours(1));
            order.VerifyBoundaryScopeAndCurrentInventory(boundaryId, mapId, count,
                AdminId, Now.AddHours(5));
        }
        order.ConfirmPoleCountAndPricing(mapId, count,
            baseline ? SurveyPoleCountSource.BaselinePublication : SurveyPoleCountSource.CurrentPublishedInventory,
            "Verified inventory", Guid.NewGuid(), PricePerPole.Create(unitPrice),
            CurrencyCode.Vnd, AdminId, Now.AddHours(7));
        return order;
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void SurveyWithoutComboKeepsCatalogPriceAndFullBalance(double? discountRaw)
    {
        decimal? discount = discountRaw.HasValue ? (decimal)discountRaw.Value : null;
        var order = SurveyOrder.Create(
            "ORD-NO-COMBO", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), false, null, "v1", "Existing farm", Now,
            customDepositAmount: 0m, customComboDiscountPercentage: discount);
        var mapId = Guid.NewGuid();
        order.VerifyBoundaryScopeAndCurrentInventory(
            Guid.NewGuid(), mapId, ConfirmedSurveyPoleCount.Create(1000),
            AdminId, Now.AddHours(1));
        order.ConfirmPoleCountAndPricing(
            mapId, ConfirmedSurveyPoleCount.Create(1000),
            SurveyPoleCountSource.CurrentPublishedInventory, "Verified inventory",
            Guid.NewGuid(), PricePerPole.Create(2500m), CurrencyCode.Vnd,
            AdminId, Now.AddHours(2));

        Assert.Equal(discount, order.ComboDiscountPercentage);
        Assert.Equal(2500m, order.PricePerPoleSnapshot?.Amount);
        Assert.Equal(2500000m, order.FinalPrice);
        Assert.Equal(2500000m, order.RemainingAmount);
        Assert.Equal(order.OriginalFinalPrice, order.FinalPrice);
    }

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
    [InlineData(100)]
    [InlineData(99.999)]
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

    [Fact]
    public void SurveyOrderCalculatesComboDiscountCorrectlyForFollowUpComboOrder()
    {
        // Arrange: Đơn Follow-up trên nông trại đã có map (requiresBaselineMapping: false)
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var boundaryVersionId = Guid.NewGuid();
        var baseMapVersionId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();

        var order = SurveyOrder.Create(
            "ORD-FOLLOWUP-COMBO",
            tenantId,
            farmId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping: false,
            previousCompatibleOrderId: Guid.NewGuid(),
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "Follow-up survey",
            createdAt: Now,
            customDepositAmount: 0m,
            customComboDiscountPercentage: 20.00m);

        Assert.Equal(20.00m, order.ComboDiscountPercentage);
        Assert.Equal(0m, order.DepositAmount);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);

        // Chuyển trạng thái xác minh ranh giới & kiểm kê sang AwaitingPricing
        order.VerifyBoundaryScopeAndCurrentInventory(
            farmBoundaryVersionId: boundaryVersionId,
            farmBaseMapVersionId: baseMapVersionId,
            confirmedPoleCount: ConfirmedSurveyPoleCount.Create(1050),
            verifiedBy: AdminId,
            verifiedAt: Now.AddHours(1));

        Assert.Equal(SurveyOrderStatus.AwaitingPricing, order.Status);

        // Act: Chốt giá cho 1.050 trụ (sau biến động) với đơn giá 2.500 VND
        order.ConfirmPoleCountAndPricing(
            farmBaseMapVersionId: baseMapVersionId,
            confirmedPoleCount: ConfirmedSurveyPoleCount.Create(1050),
            poleCountSource: SurveyPoleCountSource.CurrentPublishedInventory,
            evidenceReference: "Follow-up inventory verification",
            surveyServicePriceId: priceVersionId,
            pricePerPole: PricePerPole.Create(2_500m),
            currency: CurrencyCode.Vnd,
            confirmedBy: AdminId,
            confirmedAt: Now.AddHours(2));

        // Assert:
        // Giá gốc danh mục: 1.050 * 2.500 = 2.625.000 VND
        Assert.Equal(2_500m, order.OriginalPricePerPoleSnapshot?.Amount);
        Assert.Equal(2_625_000m, order.OriginalFinalPrice);

        // Đơn giá sau chiết khấu 20%: 2.500 * (1 - 0.20) = 2.000 VND
        Assert.Equal(2_000m, order.PricePerPoleSnapshot?.Amount);

        // Tổng tiền cuối cùng: 1.050 * 2.000 = 2.100.000 VND
        Assert.Equal(2_100_000m, order.FinalPrice);

        // Số tiền còn lại phải thanh toán: 2.100.000 VND (vì cọc = 0)
        Assert.Equal(2_100_000m, order.RemainingAmount);
        Assert.Equal(SurveyOrderStatus.AwaitingPaidAppointment, order.Status);
    }
}
