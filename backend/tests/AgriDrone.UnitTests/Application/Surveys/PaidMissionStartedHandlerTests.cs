using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class PaidMissionStartedHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartedPaidMissionMovesOrderToInProgress()
    {
        await using var context = CreateContext();
        var order = CreateReadyOrder();
        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var handler = new PaidMissionStartedHandler(
            context, new InboxExecutionCoordinator(TimeProvider.System));
        var result = await handler.HandleAsync(CreateEnvelope(order), CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        Assert.Equal(SurveyOrderStatus.InProgress,
            (await context.SurveyOrders.SingleAsync(item => item.Id == order.Id)).Status);
    }

    [Fact]
    public async Task StartedPaidMissionIsIdempotentWhenOrderIsInProgress()
    {
        await using var context = CreateContext();
        var order = CreateReadyOrder();
        order.StartPaidService(Now.AddHours(8));
        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var handler = new PaidMissionStartedHandler(
            context, new InboxExecutionCoordinator(TimeProvider.System));
        var result = await handler.HandleAsync(CreateEnvelope(order), CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        Assert.Equal(SurveyOrderStatus.InProgress, order.Status);
    }

    private static IntegrationEventEnvelope<PaidMissionStartedV1> CreateEnvelope(
        SurveyOrder order)
    {
        var startedAt = Now.AddHours(8);
        return IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.PaidMissionStartedV1,
            Guid.NewGuid(), Guid.NewGuid(), order.TenantId, Guid.NewGuid(),
            startedAt,
            new PaidMissionStartedV1(
                order.Id, Guid.NewGuid(), order.TenantId, order.FarmId, startedAt));
    }

    private static SurveyOrder CreateReadyOrder()
    {
        var order = SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            requiresBaselineMapping: true,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "test.v1",
            previousOrderCompatibilityReason: "NO_PREVIOUS_PUBLISHED_ORDER",
            createdAt: Now);
        var managerId = Guid.NewGuid();
        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 5_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(Guid.NewGuid(), managerId, Now.AddHours(3));
        order.RecordBaselinePublication(
            Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(80),
            managerId, Now.AddHours(4));
        order.ConfirmPricing(
            Guid.NewGuid(), PricePerPole.Create(20_000m), CurrencyCode.Vnd,
            managerId, Now.AddHours(5));
        order.MarkPaidAppointmentConfirmed(Now.AddHours(6));
        order.MarkPaymentConfirmed(Now.AddHours(7));
        return order;
    }

    private static SurveysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"paid-mission-handler-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SurveysDbContext(options);
    }
}
