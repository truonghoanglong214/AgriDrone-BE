using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyDepositPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void CreateWithValidParametersSetsPropertiesCorrectly()
    {
        // Arrange & Act
        var policy = LegacySurveyDepositPolicy.Create(
            versionNumber: 1,
            amountVnd: 500_000m,
            createdBy: AdminId,
            effectiveFrom: Now);

        // Assert
        Assert.NotEqual(Guid.Empty, policy.Id);
        Assert.Equal(1, policy.VersionNumber);
        Assert.Equal(500_000m, policy.AmountVnd);
        Assert.Equal(AdminId, policy.CreatedBy);
        Assert.Equal(Now, policy.EffectiveFrom);
        Assert.Null(policy.EffectiveTo);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(5000.50)]
    public void CreateWithInvalidAmountThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacySurveyDepositPolicy.Create(1, invalidAmount, AdminId, Now));
    }

    [Fact]
    public void ZeroDepositPolicyCreatesFollowUpOrderWithoutDepositPayment()
    {
        var policy = LegacySurveyDepositPolicy.Create(1, 0m, AdminId, Now);
        var order = SurveyOrder.Create(
            "ORD-ZERO-DEPOSIT", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), false, null, "v1", "EXISTING_BASEMAP", Now,
            customDepositAmount: policy.AmountVnd);

        Assert.Equal(0m, order.DepositAmount);
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        Assert.Null(order.DepositPaymentId);
    }

    [Fact]
    public void CloseAtSetsEffectiveToWhenValid()
    {
        // Arrange
        var policy = LegacySurveyDepositPolicy.Create(1, 500_000m, AdminId, Now);
        var closeTime = Now.AddDays(7);

        // Act
        policy.CloseAt(closeTime);

        // Assert
        Assert.Equal(closeTime, policy.EffectiveTo);
    }

    [Fact]
    public void CloseAtThrowsInvalidOperationExceptionWhenClosingBeforeEffectiveFrom()
    {
        var policy = LegacySurveyDepositPolicy.Create(1, 500_000m, AdminId, Now);
        var invalidCloseTime = Now.AddMinutes(-5);

        Assert.Throws<InvalidOperationException>(() =>
            policy.CloseAt(invalidCloseTime));
    }

    [Fact]
    public void SurveyOrderCreateUsesDynamicDepositAmountWhenProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        const decimal dynamicDeposit = 200_000m;

        // Act
        var order = SurveyOrder.Create(
            orderNumber: "ORD-2026-TEST",
            tenantId: tenantId,
            farmId: farmId,
            surveyRequestId: requestId,
            surveyServiceId: serviceId,
            requiresBaselineMapping: true,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "NEW",
            createdAt: Now,
            customDepositAmount: dynamicDeposit);

        // Assert
        Assert.Equal(dynamicDeposit, order.DepositAmount);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineAppointment, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
    }

    [Fact]
    public void SurveyOrderCreateFallsBackToDefaultWhenDynamicDepositNotProvided()
    {
        var order = SurveyOrder.Create(
            orderNumber: "ORD-2026-DEFAULT",
            tenantId: Guid.NewGuid(),
            farmId: Guid.NewGuid(),
            surveyRequestId: Guid.NewGuid(),
            surveyServiceId: Guid.NewGuid(),
            requiresBaselineMapping: true,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "NEW",
            createdAt: Now);

        Assert.Equal(SurveyOrder.DefaultBaselineDepositAmountVnd, order.DepositAmount);
    }

    [Fact]
    public void SurveyOrderCreateAllowsFollowUpOrderWithDynamicDeposit()
    {
        const decimal dynamicDeposit = 300_000m;

        var order = SurveyOrder.Create(
            orderNumber: "ORD-2026-FOLLOWUP-DEP",
            tenantId: Guid.NewGuid(),
            farmId: Guid.NewGuid(),
            surveyRequestId: Guid.NewGuid(),
            surveyServiceId: Guid.NewGuid(),
            requiresBaselineMapping: false,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "EXISTING_BASEMAP",
            createdAt: Now,
            customDepositAmount: dynamicDeposit);

        Assert.Equal(dynamicDeposit, order.DepositAmount);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, order.Status);
        Assert.Equal(SurveyPaymentStatus.Pending, order.DepositPaymentStatus);
    }

    [Fact]
    public void FollowUpOrderRecordsDepositConfirmedAndAppliesCreditToPricing()
    {
        const decimal depositAmount = 500_000m;
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var baseMapId = Guid.NewGuid();

        var order = SurveyOrder.Create(
            orderNumber: "ORD-2026-FOLLOWUP-PAID-DEP",
            tenantId: Guid.NewGuid(),
            farmId: Guid.NewGuid(),
            surveyRequestId: Guid.NewGuid(),
            surveyServiceId: Guid.NewGuid(),
            requiresBaselineMapping: false,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "EXISTING_BASEMAP",
            createdAt: Now,
            customDepositAmount: depositAmount);

        // Khách hàng nộp cọc trước
        order.RecordFollowUpDepositConfirmed(paymentId, depositAmount, Now.AddHours(1));
        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        Assert.Equal(paymentId, order.DepositPaymentId);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, order.Status);

        // Staff xác minh boundary và inventory hiện có
        order.VerifyBoundaryScopeAndCurrentInventory(
            boundaryId,
            baseMapId,
            ConfirmedSurveyPoleCount.Create(1000),
            managerId,
            Now.AddHours(2));
        Assert.Equal(SurveyOrderStatus.AwaitingPricing, order.Status);

        // Staff chốt giá: 1.000 trụ * 2.000 đ = 2.000.000 đ
        order.ConfirmPricing(
            priceId,
            PricePerPole.Create(2_000m),
            CurrencyCode.Vnd,
            managerId,
            Now.AddHours(3));

        Assert.Equal(2_000_000m, order.FinalPrice);
        // Đã cấn trừ 500.000 đ tiền cọc
        Assert.Equal(1_500_000m, order.RemainingAmount);
        Assert.Equal(SurveyOrderStatus.AwaitingPaidAppointment, order.Status);
    }

    [Fact]
    public void FollowUpOrderPricingConfirmedFirstThenDepositPaidRecalculatesRemainingAmount()
    {
        const decimal depositAmount = 500_000m;
        var paymentId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var baseMapId = Guid.NewGuid();

        var order = SurveyOrder.Create(
            orderNumber: "ORD-2026-FOLLOWUP-DEP-LATE",
            tenantId: Guid.NewGuid(),
            farmId: Guid.NewGuid(),
            surveyRequestId: Guid.NewGuid(),
            surveyServiceId: Guid.NewGuid(),
            requiresBaselineMapping: false,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "v1",
            previousOrderCompatibilityReason: "EXISTING_BASEMAP",
            createdAt: Now,
            customDepositAmount: depositAmount);

        order.VerifyBoundaryScopeAndCurrentInventory(
            boundaryId,
            baseMapId,
            ConfirmedSurveyPoleCount.Create(1000),
            managerId,
            Now.AddHours(1));

        // Chốt giá trước khi nộp cọc: 1.000 trụ * 2.000 đ = 2.000.000 đ
        order.ConfirmPricing(
            priceId,
            PricePerPole.Create(2_000m),
            CurrencyCode.Vnd,
            managerId,
            Now.AddHours(2));

        Assert.Equal(2_000_000m, order.FinalPrice);
        // Vì cọc chưa nộp nên RemainingAmount ban đầu là full 2.000.000 đ
        Assert.Equal(2_000_000m, order.RemainingAmount);

        // Khách hàng nộp cọc sau khi chốt giá
        order.RecordFollowUpDepositConfirmed(paymentId, depositAmount, Now.AddHours(3));

        Assert.Equal(SurveyPaymentStatus.Paid, order.DepositPaymentStatus);
        // RemainingAmount tự động trừ đi 500.000 đ cọc còn 1.500.000 đ
        Assert.Equal(1_500_000m, order.RemainingAmount);
    }
}
