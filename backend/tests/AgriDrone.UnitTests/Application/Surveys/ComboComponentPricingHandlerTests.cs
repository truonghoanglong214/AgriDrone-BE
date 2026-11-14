using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class ComboComponentPricingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmationRequiresIndependentMappingCountAndPublishesV2(bool hasMappingCount)
    {
        var order = SurveyOrder.Create("SPLIT-SCOPE", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            false, null, "components.v1", "FOLLOW_UP", Now, 500000m, 20m);
        order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 2500m, 1500m, 1000m);
        order.RecordFollowUpDepositConfirmed(Guid.NewGuid(), 500000m, Now);
        var map = Guid.NewGuid();
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), map, ConfirmedSurveyPoleCount.Create(1000), Actor, Now);
        order.MarkPaidAppointmentConfirmed(Now); order.StartPaidService(Now); order.MarkAnalysisReady(Now);
        order.RecordReviewedServicePublication(Now);
        var state = new SurveyPricingConfirmationState(order, Guid.NewGuid(), map, Guid.NewGuid(), 300,
            order.BookedServicePriceId!.Value, 2500m, "VND", hasMappingCount ? 1000 : null);
        var outbox = new Outbox();
        var handler = new ConfirmSurveyOrderPoleCountCommandHandler(new UnitOfWork(), new Repository(state),
            outbox, new Audit(), new Context(), new Clock());

        var result = await handler.Handle(new ConfirmSurveyOrderPoleCountCommand(order.Id, map, "reviewed scope", order.Version), default);

        if (!hasMappingCount)
        {
            Assert.True(result.IsFailure);
            Assert.Equal("SurveyPricing.MappingCountRequired", result.Error.Code);
            Assert.Null(order.FinalPrice);
            Assert.Empty(outbox.Messages);
            return;
        }
        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1000, result.Value.ConfirmedMappingPoleCount);
        Assert.Equal(300, result.Value.ConfirmedPlantHealthPoleCount);
        Assert.Equal(1440000m, result.Value.FinalPrice);
        Assert.Equal(940000m, result.Value.RemainingAmount);
        var envelope = Assert.IsType<IntegrationEventEnvelope<SurveyOrderPricingConfirmedV2>>(Assert.Single(outbox.Messages));
        Assert.Equal("survey.order-pricing-confirmed.v2", envelope.EventType);
        Assert.Equal(2, envelope.SchemaVersion);
        Assert.Equal(1500m, envelope.Payload.MappingPricePerPole);
        Assert.Equal(1000m, envelope.Payload.PlantHealthPricePerPole);
        Assert.Equal(1000, envelope.Payload.ConfirmedMappingPoleCount);
        Assert.Equal(300, envelope.Payload.ConfirmedPlantHealthPoleCount);
    }

    private sealed class Repository(SurveyPricingConfirmationState state) : ISurveyPricingConfirmationRepository
    {
        public Task<Result<SurveyPricingConfirmationState>> GetForConfirmationAsync(Guid id, Guid actor,
            DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(state));
    }
    private sealed class UnitOfWork : ISurveyPricingConfirmationUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void AddAuditLog(AuditLog log) { }
    }
    private sealed class Outbox : ISurveyPricingConfirmationOutbox
    {
        public List<object> Messages { get; } = [];
        public void Add<T>(IntegrationEventEnvelope<T> envelope, string? partitionKey = null) => Messages.Add(envelope);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Context : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => Actor;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
    private sealed class Audit : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) { }
        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId, Guid correlationId,
            string entityType, Guid entityId, string action, JsonDocument? oldData,
            JsonDocument? newData, DateTimeOffset createdAt) { }
    }
}
