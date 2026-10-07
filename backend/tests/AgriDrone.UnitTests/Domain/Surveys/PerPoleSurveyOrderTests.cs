using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class PerPoleSurveyOrderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PerPoleValueObjectsRequirePositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PricePerPole.Create(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ConfirmedSurveyPoleCount.Create(0));
    }

    [Fact]
    public void DefaultPerPoleValuesCannotBypassValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Money.CalculateSurveyPrice(
                default,
                PricePerPole.Create(10_000m),
                CurrencyCode.Vnd));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyServicePrice.CreatePerPole(
                Guid.NewGuid(),
                default,
                CurrencyCode.Vnd,
                Now,
                effectiveTo: null,
                createdBy: Guid.NewGuid(),
                createdAt: Now));
    }

    [Fact]
    public void CataloguePriceUsesPerPoleValueAndLeavesLegacyPriceEmpty()
    {
        var price = SurveyServicePrice.CreatePerPole(
            Guid.NewGuid(),
            PricePerPole.Create(15_000m),
            CurrencyCode.Vnd,
            Now,
            Now.AddMonths(1),
            Guid.NewGuid(),
            Now);

        Assert.Equal(15_000m, price.PricePerPole?.Amount);
        Assert.Null(price.PricePerHa);
        Assert.False(price.IsLegacyPerHectarePrice);
        Assert.True(price.IsEffectiveAt(Now.AddDays(1)));
        Assert.False(price.IsEffectiveAt(Now.AddMonths(1)));
    }

    [Fact]
    public void UnmappedFarmCompletesBaselineBeforePerPolePricing()
    {
        var order = CreateOrder(requiresBaselineMapping: true);
        var boundaryVersionId = Guid.NewGuid();
        var baseMapVersionId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();

        order.VerifyBoundaryAndScopeForUnmappedFarm(
            boundaryVersionId,
            managerId,
            Now.AddMinutes(1));
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(2));
        order.StartBaselineMapping(Now.AddMinutes(3));
        order.MarkBaselineCandidatesReady(Now.AddMinutes(4));
        order.RecordBaselinePublication(
            baseMapVersionId,
            ConfirmedSurveyPoleCount.Create(125),
            managerId,
            Now.AddMinutes(5));
        order.ConfirmPricing(
            priceVersionId,
            PricePerPole.Create(10_000m),
            CurrencyCode.Vnd,
            managerId,
            Now.AddMinutes(6));

        Assert.Equal(SurveyOrderStatus.AwaitingPaidAppointment, order.Status);
        Assert.Equal(boundaryVersionId, order.FarmBoundaryVersionId);
        Assert.Equal(baseMapVersionId, order.FarmBaseMapVersionId);
        Assert.Equal(125, order.ConfirmedSurveyPoleCount?.Value);
        Assert.Equal(10_000m, order.PricePerPoleSnapshot?.Amount);
        Assert.Equal(1_250_000m, order.FinalPrice);
        Assert.Null(order.ConfirmedSurveyAreaHa);
        Assert.Null(order.PricePerHaSnapshot);
    }

    [Fact]
    public void MappedFarmSkipsBaselineStatesButStillRequiresCountConfirmation()
    {
        var order = CreateOrder(requiresBaselineMapping: false);

        order.VerifyBoundaryScopeAndCurrentInventory(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ConfirmedSurveyPoleCount.Create(80),
            Guid.NewGuid(),
            Now.AddMinutes(1));

        Assert.Equal(SurveyOrderStatus.AwaitingPricing, order.Status);
        Assert.Equal(80, order.ConfirmedSurveyPoleCount?.Value);
        Assert.NotNull(order.PoleCountConfirmedBy);
        Assert.Equal(Now.AddMinutes(1), order.PoleCountConfirmedAt);
    }

    [Fact]
    public void UnmappedFarmCannotUseMappedFarmShortcut()
    {
        var order = CreateOrder(requiresBaselineMapping: true);

        var exception = Assert.Throws<SurveyDomainException>(() =>
            order.VerifyBoundaryScopeAndCurrentInventory(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ConfirmedSurveyPoleCount.Create(80),
                Guid.NewGuid(),
                Now.AddMinutes(1)));

        Assert.Equal(
            SurveyOrderDomainErrorCodes.BaselineMappingRequired,
            exception.Code);
        Assert.Equal(
            SurveyOrderStatus.PendingBoundaryVerification,
            order.Status);
    }

    [Fact]
    public void OrderCanCancelAfterBaselinePublicationBeforePaidMissionStarts()
    {
        var managerId = Guid.NewGuid();
        var order = CreateOrder(requiresBaselineMapping: true);
        order.VerifyBoundaryAndScopeForUnmappedFarm(
            Guid.NewGuid(),
            managerId,
            Now.AddMinutes(1));
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(2));
        order.StartBaselineMapping(Now.AddMinutes(3));
        order.MarkBaselineCandidatesReady(Now.AddMinutes(4));
        order.RecordBaselinePublication(
            Guid.NewGuid(),
            ConfirmedSurveyPoleCount.Create(20),
            managerId,
            Now.AddMinutes(5));

        order.Cancel(Now.AddMinutes(6));

        Assert.Equal(SurveyOrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(SurveyAppointmentPurpose.BaselineMapping)]
    [InlineData(SurveyAppointmentPurpose.PaidService)]
    public void AppointmentCarriesExplicitPurpose(
        SurveyAppointmentPurpose purpose)
    {
        var appointment = SurveyAppointment.Create(
            Guid.NewGuid(),
            purpose,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(2),
            Now);

        Assert.Equal(purpose, appointment.Purpose);
        Assert.Equal(SurveyAppointmentStatus.Proposed, appointment.Status);
    }

    private static SurveyOrder CreateOrder(bool requiresBaselineMapping) =>
        SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping,
            previousCompatibleOrderId: null,
            createdAt: Now);
}
