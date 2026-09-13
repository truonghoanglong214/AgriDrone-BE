using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarmById;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class GetArchivedFarmQueryHandlerTests
{
    [Fact]
    public async Task GetArchivedFarmsReturnsTenantScopedPageForOwner()
    {
        var tenantId = Guid.NewGuid();
        var farm = CreateArchivedFarm(tenantId);
        var queries = new FakeFarmQueries
        {
            ArchivedPage = new PagedResult<ArchivedFarmResponse>(
                [farm],
                PageNumber: 2,
                PageSize: 10,
                TotalCount: 11)
        };
        var access = new FakeEffectiveAccessService();
        var handler = new GetArchivedFarmsQueryHandler(
            queries,
            new FakeExecutionContext(tenantId, Guid.NewGuid()),
            access);

        var result = await handler.Handle(
            new GetArchivedFarmsQuery(2, 10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(farm.Version, result.Value.Items[0].Version);
        Assert.Equal(tenantId, queries.RequestedTenantId);
        Assert.Equal(2, queries.RequestedPage?.PageNumber);
        Assert.Equal(10, queries.RequestedPage?.PageSize);
        Assert.Equal(TenantAccessLevel.Owner, access.RequiredAccess);
    }

    [Fact]
    public async Task GetArchivedFarmByIdReturnsArchivedFarmWithVersion()
    {
        var tenantId = Guid.NewGuid();
        var farm = CreateArchivedFarm(tenantId);
        var queries = new FakeFarmQueries { ArchivedFarm = farm };
        var handler = new GetArchivedFarmByIdQueryHandler(
            queries,
            new FakeExecutionContext(tenantId, Guid.NewGuid()),
            new FakeEffectiveAccessService());

        var result = await handler.Handle(
            new GetArchivedFarmByIdQuery(farm.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(farm.Id, result.Value.Id);
        Assert.Equal(farm.ArchivedAt, result.Value.ArchivedAt);
        Assert.Equal(farm.Version, result.Value.Version);
        Assert.Equal(farm.Id, queries.RequestedFarmId);
        Assert.Equal(tenantId, queries.RequestedTenantId);
    }

    [Fact]
    public async Task GetArchivedFarmByIdReturnsNotFoundWhenFarmIsUnavailable()
    {
        var tenantId = Guid.NewGuid();
        var handler = new GetArchivedFarmByIdQueryHandler(
            new FakeFarmQueries(),
            new FakeExecutionContext(tenantId, Guid.NewGuid()),
            new FakeEffectiveAccessService());

        var result = await handler.Handle(
            new GetArchivedFarmByIdQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetArchivedFarmsRejectsNonOwnerBeforeQueryingData()
    {
        var queries = new FakeFarmQueries();
        var access = new FakeEffectiveAccessService
        {
            Decision = AccessDecision.Deny(
                AccessDenialReason.TenantRoleInsufficient)
        };
        var handler = new GetArchivedFarmsQueryHandler(
            queries,
            new FakeExecutionContext(Guid.NewGuid(), Guid.NewGuid()),
            access);

        var result = await handler.Handle(
            new GetArchivedFarmsQuery(1, 20),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.AccessDenied", result.Error.Code);
        Assert.Null(queries.RequestedTenantId);
    }

    private static ArchivedFarmResponse CreateArchivedFarm(Guid tenantId) =>
        new(
            Guid.NewGuid(),
            tenantId,
            "FARM-001",
            "Archived farm",
            Address: null,
            Boundary: null,
            CenterPoint: null,
            AreaHectares: 10,
            GeneralStatus.Inactive,
            DateTimeOffset.UtcNow.AddYears(-1),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1),
            Version: 3);

    private sealed class FakeFarmQueries : IFarmQueries
    {
        public PagedResult<ArchivedFarmResponse> ArchivedPage { get; set; } =
            new([], 1, 20, 0);

        public ArchivedFarmResponse? ArchivedFarm { get; set; }
        public Guid? RequestedTenantId { get; private set; }
        public Guid? RequestedFarmId { get; private set; }
        public PagedRequest? RequestedPage { get; private set; }

        public Task<PagedResult<ArchivedFarmResponse>> GetArchivedFarmsPageAsync(
            Guid tenantId,
            PagedRequest pagedRequest,
            CancellationToken cancellationToken)
        {
            RequestedTenantId = tenantId;
            RequestedPage = pagedRequest;
            return Task.FromResult(ArchivedPage);
        }

        public Task<ArchivedFarmResponse?> GetArchivedFarmByIdAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken)
        {
            RequestedTenantId = tenantId;
            RequestedFarmId = farmId;
            return Task.FromResult(ArchivedFarm);
        }

        public Task<PagedResult<
            AgriDrone.Modules.Farms.Application.Features.GetFarm.FarmListItemResponse>>
            GetFarmsPageAsync(
                Guid tenantId,
                PagedRequest pagedRequest,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } = AccessDecision.Allow();
        public TenantAccessLevel? RequiredAccess { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            RequiredAccess = requiredAccess;
            return Task.FromResult(Decision);
        }

        public Task<AccessDecision> CheckFarmAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessDecision> CheckZoneAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeExecutionContext(Guid tenantId, Guid actorId)
        : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
