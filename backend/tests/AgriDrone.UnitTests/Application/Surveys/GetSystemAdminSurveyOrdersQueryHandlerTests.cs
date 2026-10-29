using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSystemAdminSurveyOrders;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Pagination;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class GetSystemAdminSurveyOrdersQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsAllSurveyOrdersWithFilters()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var queries = new FakeAdminQueries(new PagedResult<SystemManagerSurveyOrderItem>(
            [new SystemManagerSurveyOrderItem(
                orderId,
                "SO-ADMIN-001",
                tenantId,
                farmId,
                "FARM-ADM",
                "Admin Farm",
                Guid.NewGuid(),
                "PLANT_HEALTH",
                "Plant health",
                SurveyOrderStatus.PendingBoundaryVerification,
                1,
                true,
                null,
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow)],
            1,
            20,
            1));

        var handler = new GetSystemAdminSurveyOrdersQueryHandler(queries);

        var result = await handler.Handle(
            new GetSystemAdminSurveyOrdersQuery(
                SurveyOrderStatus.PendingBoundaryVerification,
                tenantId,
                farmId,
                1,
                20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(queries.CapturedFilter);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, queries.CapturedFilter.Status);
        Assert.Equal(tenantId, queries.CapturedFilter.TenantId);
        Assert.Equal(farmId, queries.CapturedFilter.FarmId);

        var item = Assert.Single(result.Value.Items);
        Assert.Equal(orderId, item.SurveyOrderId);
        Assert.Equal("SO-ADMIN-001", item.OrderNumber);
        Assert.Equal((uint)1, item.OrderVersion);
    }

    private sealed class FakeAdminQueries(
        PagedResult<SystemManagerSurveyOrderItem> response)
        : ISystemManagerSurveyOrderQueries
    {
        public SystemAdminSurveyOrderFilter? CapturedFilter { get; private set; }

        public Task<PagedResult<SystemManagerSurveyOrderItem>?> GetAssignedAsync(
            Guid actorId,
            SystemManagerSurveyOrderFilter filter,
            PagedRequest page,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<SystemManagerSurveyOrderItem>> GetAllAsync(
            SystemAdminSurveyOrderFilter filter,
            PagedRequest page,
            CancellationToken cancellationToken = default)
        {
            CapturedFilter = filter;
            return Task.FromResult(response);
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
}
