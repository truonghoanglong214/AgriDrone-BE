using System.Text.Json;
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

public sealed class BaselineLifecycleSyncHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BaselineMissionStartedHandlerTransitionsOrderToBaselineInProgress()
    {
        await using var context = CreateContext();
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        Assert.Equal(SurveyOrderStatus.BaselineReady, order.Status);

        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);
        var handler = new BaselineMissionStartedHandler(context, coordinator);

        var missionId = Guid.NewGuid();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineMissionStartedV1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.TenantId,
            Guid.NewGuid(),
            Now.AddHours(1),
            new BaselineMissionStartedV1(
                order.Id,
                missionId,
                order.TenantId,
                order.FarmId,
                Now.AddHours(1)));

        var result = await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        var reloaded = await context.SurveyOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, reloaded.Status);
    }

    [Fact]
    public async Task BaselineMissionStartedHandlerIsIdempotentWhenAlreadyInProgress()
    {
        await using var context = CreateContext();
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, order.Status);

        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);
        var handler = new BaselineMissionStartedHandler(context, coordinator);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineMissionStartedV1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.TenantId,
            Guid.NewGuid(),
            Now.AddHours(1),
            new BaselineMissionStartedV1(
                order.Id,
                Guid.NewGuid(),
                order.TenantId,
                order.FarmId,
                Now.AddHours(1)));

        var result = await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        var reloaded = await context.SurveyOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, reloaded.Status);
    }

    [Fact]
    public async Task BaselineOrthophotoCompletedHandlerTransitionsOrderToAwaitingBaselineReview()
    {
        await using var context = CreateContext();
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        Assert.Equal(SurveyOrderStatus.BaselineInProgress, order.Status);

        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);
        var handler = new BaselineOrthophotoCompletedHandler(context, coordinator);

        var missionId = Guid.NewGuid();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineOrthophotoCompletedV1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.TenantId,
            Guid.NewGuid(),
            Now.AddHours(2),
            new BaselineOrthophotoCompletedV1(
                order.Id,
                missionId,
                order.TenantId,
                order.FarmId,
                "https://minio.agridrone.internal/orthophoto.tif",
                Now.AddHours(2)));

        var result = await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        var reloaded = await context.SurveyOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, reloaded.Status);
    }

    [Fact]
    public async Task BaselineOrthophotoCompletedHandlerIsIdempotentWhenAlreadyAwaitingReview()
    {
        await using var context = CreateContext();
        var order = CreateOrder(requiresBaselineMapping: true);
        var paymentId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(paymentId, 500_000m, Now.AddMinutes(5));
        order.StartBaselineMapping(Now.AddHours(1));
        order.MarkBaselineCandidatesReady(Now.AddHours(2));
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, order.Status);

        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);
        var handler = new BaselineOrthophotoCompletedHandler(context, coordinator);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineOrthophotoCompletedV1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.TenantId,
            Guid.NewGuid(),
            Now.AddHours(2),
            new BaselineOrthophotoCompletedV1(
                order.Id,
                Guid.NewGuid(),
                order.TenantId,
                order.FarmId,
                null,
                Now.AddHours(2)));

        var result = await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.Acknowledge, result.Disposition);
        var reloaded = await context.SurveyOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, reloaded.Status);
    }

    [Fact]
    public async Task HandlerRejectsWhenTenantOrFarmMismatched()
    {
        await using var context = CreateContext();
        var order = CreateOrder(requiresBaselineMapping: true);
        context.SurveyOrders.Add(order);
        await context.SaveChangesAsync();

        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);
        var handler = new BaselineMissionStartedHandler(context, coordinator);

        var wrongFarmId = Guid.NewGuid();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineMissionStartedV1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.TenantId,
            Guid.NewGuid(),
            Now.AddHours(1),
            new BaselineMissionStartedV1(
                order.Id,
                Guid.NewGuid(),
                order.TenantId,
                wrongFarmId,
                Now.AddHours(1)));

        var result = await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(IntegrationMessageDisposition.DeadLetter, result.Disposition);
    }

    private static SurveysDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SurveysDbContext>()
            .UseInMemoryDatabase($"surveys-handler-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SurveysDbContext(options);
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
            previousOrderCompatibilityPolicyVersion: "test.v1",
            previousOrderCompatibilityReason: "NO_PREVIOUS_PUBLISHED_ORDER",
            createdAt: Now);
}
