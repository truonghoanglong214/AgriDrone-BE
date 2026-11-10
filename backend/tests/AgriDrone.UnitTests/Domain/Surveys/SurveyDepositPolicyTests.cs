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
        var policy = SurveyDepositPolicy.Create(
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
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(5000.50)]
    public void CreateWithInvalidAmountThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositPolicy.Create(1, invalidAmount, AdminId, Now));
    }

    [Fact]
    public void CloseAtSetsEffectiveToWhenValid()
    {
        // Arrange
        var policy = SurveyDepositPolicy.Create(1, 500_000m, AdminId, Now);
        var closeTime = Now.AddDays(7);

        // Act
        policy.CloseAt(closeTime);

        // Assert
        Assert.Equal(closeTime, policy.EffectiveTo);
    }

    [Fact]
    public void CloseAtThrowsInvalidOperationExceptionWhenClosingBeforeEffectiveFrom()
    {
        var policy = SurveyDepositPolicy.Create(1, 500_000m, AdminId, Now);
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
}
