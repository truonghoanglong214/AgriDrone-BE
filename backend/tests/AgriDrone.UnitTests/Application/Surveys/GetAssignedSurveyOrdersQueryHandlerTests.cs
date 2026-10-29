using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetAssignedSurveyOrders;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class GetAssignedSurveyOrdersQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsOnlyTheCurrentManagersAssignedOrders()
    {
        var actorId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var queries = new FakeQueries(new PagedResult<SystemManagerSurveyOrderItem>(
            [new SystemManagerSurveyOrderItem(
                orderId,
                "SO-001",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "FARM-01",
                "Farm 01",
                Guid.NewGuid(),
                "PLANT_HEALTH",
                "Plant health",
                SurveyOrderStatus.PendingBoundaryVerification,
                3,
                true,
                null,
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow)],
            1,
            20,
            1));
        var handler = new GetAssignedSurveyOrdersQueryHandler(
            queries,
            new FakeExecutionContext(actorId));

        var result = await handler.Handle(
            new GetAssignedSurveyOrdersQuery(
                SurveyOrderStatus.PendingBoundaryVerification),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(actorId, queries.ActorId);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification,
            queries.Filter?.Status);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(orderId, item.SurveyOrderId);
        Assert.Equal((uint)3, item.OrderVersion);
    }

    [Fact]
    public async Task HandleReturnsForbiddenWhenManagerProfileCannotOperate()
    {
        var queries = new FakeQueries(response: null);
        var handler = new GetAssignedSurveyOrdersQueryHandler(
            queries,
            new FakeExecutionContext(Guid.NewGuid()));

        var result = await handler.Handle(
            new GetAssignedSurveyOrdersQuery(null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyOrderError.ForbiddenCode, result.Error.Code);
    }

    private sealed class FakeQueries(
        PagedResult<SystemManagerSurveyOrderItem>? response)
        : ISystemManagerSurveyOrderQueries
    {
        public Guid? ActorId { get; private set; }
        public SystemManagerSurveyOrderFilter? Filter { get; private set; }

        public Task<PagedResult<SystemManagerSurveyOrderItem>?> GetAssignedAsync(
            Guid actorId,
            SystemManagerSurveyOrderFilter filter,
            PagedRequest page,
            CancellationToken cancellationToken = default)
        {
            ActorId = actorId;
            Filter = filter;
            return Task.FromResult(response);
        }

        public Task<PagedResult<SystemManagerSurveyOrderItem>> GetAllAsync(
            SystemAdminSurveyOrderFilter filter,
            PagedRequest page,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response ?? new PagedResult<SystemManagerSurveyOrderItem>([], 1, 20, 0));
        }

        public Task<PagedResult<TenantOwnerSurveyOrderItem>> GetTenantOwnerPageAsync(
            Guid tenantId, SurveyOrderStatus? status, PagedRequest page,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<TenantOwnerSurveyOrderItem>(
                [], page.PageNumber, page.PageSize, 0));

        public Task<TenantOwnerSurveyOrderItem?> GetTenantOwnerByIdAsync(
            Guid tenantId, Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantOwnerSurveyOrderItem?>(null);
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
}
