using System.Reflection;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class CreateSurveyServicePriceCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplacementUsesServerTimeAndLeavesNewPriceOpen()
    {
        var actorId = Guid.NewGuid();
        var service = SurveyService.Create(
            "PLANT_HEALTH",
            "Plant health",
            "Plant health survey",
            SurveyServiceType.PlantHealth,
            Now.AddDays(-2));
        var previousPrice = SurveyServicePrice.CreatePerPole(
            service.Id,
            PricePerPole.Create(10_000m),
            CurrencyCode.Vnd,
            Now.AddDays(-1),
            effectiveTo: null,
            actorId,
            Now.AddDays(-1));
        service.AddPriceVersion(
            previousPrice,
            Now.AddDays(-1),
            expectedVersion: 0);
        SetServiceVersion(service, 1);

        var repository = new FakeSurveyServiceRepository(service);
        var catalogueQueries = new FakeSurveyCatalogueQueries();
        var unitOfWork = new FakeSurveysUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var handler = new CreateSurveyServicePriceCommandHandler(
            repository,
            catalogueQueries,
            unitOfWork,
            auditWriter,
            new FakeExecutionContext(actorId),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new CreateSurveyServicePriceCommand(
                service.Id,
                15_000m,
                "VND",
                ExpectedServiceVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, previousPrice.EffectiveTo);
        var newPrice = Assert.Single(
            service.Prices,
            price => price.Id != previousPrice.Id);
        Assert.Equal(Now, newPrice.EffectiveFrom);
        Assert.Null(newPrice.EffectiveTo);
        Assert.Equal(15_000m, newPrice.PricePerPole!.Value.Amount);
        Assert.Equal(Now, service.UpdatedAt);
        Assert.Equal(Now, catalogueQueries.EffectiveFrom);
        Assert.Null(catalogueQueries.EffectiveTo);
        Assert.Equal(previousPrice.Id, catalogueQueries.ExcludingPriceId);
        Assert.Equal(2, unitOfWork.SaveCallCount);
        Assert.Same(newPrice, repository.AddedPrice);
        Assert.Equal(Now, auditWriter.CreatedAt);
        Assert.Equal(newPrice.Id, result.Value.PriceVersionId);
    }

    private static void SetServiceVersion(SurveyService service, uint version) =>
        typeof(SurveyService)
            .GetProperty(
                nameof(SurveyService.Version),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(service, version);

    private sealed class FakeSurveyServiceRepository(SurveyService service)
        : ISurveyServiceRepository
    {
        public SurveyServicePrice? AddedPrice { get; private set; }

        public Task<SurveyService?> GetByIdAsync(
            Guid surveyServiceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyService?>(
                surveyServiceId == service.Id ? service : null);

        public Task<SurveyService?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyService?>(null);

        public void Add(SurveyService surveyService) =>
            throw new NotSupportedException();

        public void AddPrice(SurveyServicePrice price) => AddedPrice = price;
    }

    private sealed class FakeSurveyCatalogueQueries : ISurveyCatalogueQueries
    {
        public DateTimeOffset? EffectiveFrom { get; private set; }
        public DateTimeOffset? EffectiveTo { get; private set; }
        public Guid? ExcludingPriceId { get; private set; }

        public Task<bool> HasOverlappingPerPolePriceAsync(
            Guid surveyServiceId,
            DateTimeOffset effectiveFrom,
            DateTimeOffset? effectiveTo,
            Guid? excludingPriceId = null,
            CancellationToken cancellationToken = default)
        {
            EffectiveFrom = effectiveFrom;
            EffectiveTo = effectiveTo;
            ExcludingPriceId = excludingPriceId;
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<PublicSurveyServiceCatalogueItem>>
            GetPublicCatalogueAsync(
                DateTimeOffset evaluatedAt,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SurveyServiceCatalogueItem>>
            GetCatalogueAsync(
                DateTimeOffset evaluatedAt,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SurveyServicePriceHistoryItem>>
            GetPriceHistoryAsync(
                Guid surveyServiceId,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EffectiveSurveyServicePrice?> GetEffectivePerPolePriceAsync(
            Guid surveyServiceId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSurveysUnitOfWork : ISurveysUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public void AddAuditLog(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public DateTimeOffset? CreatedAt { get; private set; }

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
            CreatedAt = createdAt;

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
    }

    private sealed class FakeExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
