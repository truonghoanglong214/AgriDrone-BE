using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;
using AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace AgriDrone.UnitTests.Application.Plants;

public sealed class PlantConditionCommandHandlerTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now =
        CreatedAt.AddHours(1);

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid CorrelationId = Guid.NewGuid();

    [Fact]
    public async Task CreateRejectsMissingAuthenticatedAdministrator()
    {
        var repository = new FakePlantConditionRepository(condition: null);
        var unitOfWork = new FakePlantsUnitOfWork();
        var handler = new CreatePlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(actorId: null, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreatePlantConditionCommand(
                "DISEASE",
                "Disease",
                null,
                ConditionType.Disease,
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantCondition.CurrentUserRequired",
            result.Error.Code);
        Assert.Empty(repository.AddedConditions);
        Assert.Equal(0, unitOfWork.SaveCallCount);
        Assert.Empty(unitOfWork.AuditLogs);
    }

    [Fact]
    public async Task VersionRetiresCurrentAndCreatesNextRevisionAtomically()
    {
        var current = CreateCondition();
        var repository = new FakePlantConditionRepository(current);
        var unitOfWork = new FakePlantsUnitOfWork();
        var handler = new VersionPlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(ActorId, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VersionPlantConditionCommand(
                current.Id,
                "Sunburn damage",
                ScientificName: null,
                "Updated description",
                current.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(current.IsActive);
        Assert.Equal(Now, current.RetiredAt);
        Assert.Equal(2, current.Version);
        Assert.Equal(1, repository.UpdateCallCount);

        var next = Assert.Single(repository.AddedConditions);
        Assert.Equal(current.Id, next.SupersedesId);
        Assert.Equal(2, next.RevisionNumber);
        Assert.True(next.IsActive);
        Assert.Equal(1, next.Version);
        Assert.Equal(next.Id, result.Value.Id);
        Assert.Equal(1, unitOfWork.TransactionCallCount);
        Assert.Equal(2, unitOfWork.SaveCallCount);
        var audit = Assert.Single(unitOfWork.AuditLogs);
        Assert.Equal("VERSION", audit.Action);
        Assert.Equal(next.Id, audit.EntityId);
    }

    [Fact]
    public async Task VersionRejectsStaleExpectedVersion()
    {
        var current = CreateCondition();
        var repository = new FakePlantConditionRepository(current);
        var unitOfWork = new FakePlantsUnitOfWork();
        var handler = new VersionPlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(ActorId, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VersionPlantConditionCommand(
                current.Id,
                "Sunburn damage",
                ScientificName: null,
                Description: null,
                ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlantCondition.ConcurrentUpdate", result.Error.Code);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task VersionMapsDatabaseConcurrencyFailureToConflict()
    {
        var current = CreateCondition();
        var repository = new FakePlantConditionRepository(current);
        var unitOfWork = new FakePlantsUnitOfWork
        {
            SaveException = new DbUpdateConcurrencyException()
        };
        var handler = new VersionPlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(ActorId, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new VersionPlantConditionCommand(
                current.Id,
                "Sunburn damage",
                ScientificName: null,
                Description: null,
                current.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlantCondition.ConcurrentUpdate", result.Error.Code);
    }

    [Fact]
    public async Task RetireChangesActiveConditionAndSavesOnce()
    {
        var condition = CreateCondition();
        var repository = new FakePlantConditionRepository(condition);
        var unitOfWork = new FakePlantsUnitOfWork();
        var handler = new RetirePlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(ActorId, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetirePlantConditionCommand(
                condition.Id,
                condition.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(condition.IsActive);
        Assert.Equal(Now, condition.RetiredAt);
        Assert.Equal(2, condition.Version);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(1, unitOfWork.SaveCallCount);
        var audit = Assert.Single(unitOfWork.AuditLogs);
        Assert.Equal("RETIRE", audit.Action);
        Assert.Equal(condition.Id, audit.EntityId);
    }

    [Fact]
    public async Task RetireRejectsAlreadyRetiredCondition()
    {
        var condition = CreateCondition();
        condition.Retire(Now.AddMinutes(-1));
        var repository = new FakePlantConditionRepository(condition);
        var unitOfWork = new FakePlantsUnitOfWork();
        var handler = new RetirePlantConditionHandler(
            repository,
            unitOfWork,
            new FakeAuditWriter(),
            new FakeExecutionContext(ActorId, CorrelationId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RetirePlantConditionCommand(
                condition.Id,
                condition.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlantCondition.AlreadyRetired", result.Error.Code);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.SaveCallCount);
    }

    private static PlantCondition CreateCondition() =>
        PlantCondition.Create(
            "SUNBURN",
            "Sunburn",
            scientificName: null,
            ConditionType.AbioticDamage,
            "Heat damage",
            CreatedAt);

    private sealed class FakePlantConditionRepository(
        PlantCondition? condition) : IPlantConditionRepository
    {
        public List<PlantCondition> AddedConditions { get; } = [];
        public int UpdateCallCount { get; private set; }

        public Task<PlantCondition?> GetByIdAsync(
            Guid conditionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(condition);

        public Task<bool> CodeExistsAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Add(PlantCondition value) => AddedConditions.Add(value);

        public void Update(PlantCondition value) => UpdateCallCount++;
    }

    private sealed class FakePlantsUnitOfWork : IPlantsUnitOfWork
    {
        public int SaveCallCount { get; private set; }
        public int TransactionCallCount { get; private set; }
        public Exception? SaveException { get; init; }
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.FromResult(1);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCallCount++;
            return await operation(cancellationToken);
        }

        public void AddAuditLog(AuditLog auditLog)
        {
            AuditLogs.Add(auditLog);
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public void AddUserAction(
            IAuditLogSink sink,
            Guid tenantId,
            Guid? farmId,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt) =>
            throw new NotSupportedException();

        public void AddSystemAdminAction(
            IAuditLogSink sink,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt) =>
            sink.AddAuditLog(AuditLog.ForSystemAdminAction(
                actorId,
                correlationId,
                entityType,
                entityId,
                action,
                oldData,
                newData,
                createdAt));
    }

    private sealed class FakeExecutionContext(
        Guid? actorId,
        Guid correlationId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => actorId;
        public Guid CorrelationId => correlationId;
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
