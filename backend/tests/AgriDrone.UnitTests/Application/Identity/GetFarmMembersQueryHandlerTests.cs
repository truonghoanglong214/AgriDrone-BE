using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;
using AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class GetFarmMembersQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsFilteredFarmMembersPage()
    {
        var fixture = CreateFixture();
        var item = new FarmMemberListItemResponse(
            Guid.NewGuid(),
            fixture.FarmId,
            Guid.NewGuid(),
            "manager@example.com",
            "Farm Manager",
            TenantMemberRole.Member,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            [],
            GeneralStatus.Active,
            1,
            DateTimeOffset.UtcNow);
        fixture.Queries.Response = new PagedResult<FarmMemberListItemResponse>(
            [item],
            2,
            10,
            11);

        var result = await fixture.Handler.Handle(
            new GetFarmMembersQuery(
                fixture.FarmId,
                FarmMemberRole.Manager,
                GeneralStatus.Active,
                2,
                10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(fixture.Queries.Response, result.Value);
        Assert.Equal(fixture.TenantId, fixture.Queries.TenantId);
        Assert.Equal(fixture.FarmId, fixture.Queries.FarmId);
        Assert.Equal(FarmMemberRole.Manager, fixture.Queries.Role);
        Assert.Equal(GeneralStatus.Active, fixture.Queries.Status);
        Assert.Equal(2, fixture.Queries.PageRequest?.PageNumber);
        Assert.Equal(10, fixture.Queries.PageRequest?.PageSize);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutTenantAdminAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            CreateQuery(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.Queries.CallCount);
        Assert.Equal(0, fixture.FarmReferenceQuery.CallCount);
    }

    [Fact]
    public async Task HandleHidesFarmOutsideCurrentTenant()
    {
        var fixture = CreateFixture();
        fixture.FarmReferenceQuery.IsActive = false;

        var result = await fixture.Handler.Handle(
            CreateQuery(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.FarmNotFound", result.Error.Code);
        Assert.Equal(0, fixture.Queries.CallCount);
    }

    private static GetFarmMembersQuery CreateQuery(Fixture fixture) =>
        new(
            fixture.FarmId,
            null,
            GeneralStatus.Active,
            1,
            20);

    private static Fixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var queries = new FakeFarmMembershipQueries();
        var farmReferenceQuery = new FakeFarmAssignmentReferenceQuery();
        var accessService = new FakeEffectiveAccessService();
        var handler = new GetFarmMembersQueryHandler(
            queries,
            farmReferenceQuery,
            new FakeExecutionContext(tenantId, Guid.NewGuid()),
            accessService);

        return new Fixture(
            handler,
            queries,
            farmReferenceQuery,
            accessService,
            tenantId,
            farmId);
    }

    private sealed record Fixture(
        GetFarmMembersQueryHandler Handler,
        FakeFarmMembershipQueries Queries,
        FakeFarmAssignmentReferenceQuery FarmReferenceQuery,
        FakeEffectiveAccessService AccessService,
        Guid TenantId,
        Guid FarmId);

    private sealed class FakeFarmMembershipQueries : IFarmMembershipQueries
    {
        public PagedResult<FarmMemberListItemResponse> Response { get; set; } =
            new([], 1, 20, 0);

        public int CallCount { get; private set; }

        public Guid? TenantId { get; private set; }

        public Guid? FarmId { get; private set; }

        public FarmMemberRole? Role { get; private set; }

        public GeneralStatus? Status { get; private set; }

        public PagedRequest? PageRequest { get; private set; }

        public Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
            Guid tenantId,
            Guid farmId,
            Guid userId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<FarmMemberListItemResponse>>
            GetMembersPageAsync(
                Guid tenantId,
                Guid farmId,
                FarmMemberRole? role,
                GeneralStatus? status,
                PagedRequest pagedRequest,
                CancellationToken cancellationToken)
        {
            CallCount++;
            TenantId = tenantId;
            FarmId = farmId;
            Role = role;
            Status = status;
            PageRequest = pagedRequest;
            return Task.FromResult(Response);
        }

        public Task<PagedResult<MyFarmAssignmentReadModel>>
            GetMyAssignmentsPageAsync(
                Guid tenantId,
                Guid userId,
                IReadOnlyCollection<Guid> activeFarmIds,
                FarmMemberRole? role,
                PagedRequest pagedRequest,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFarmAssignmentReferenceQuery
        : IFarmAssignmentReferenceQuery
    {
        public bool IsActive { get; set; } = true;

        public int CallCount { get; private set; }

        public Task<bool> IsActiveFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(IsActive);
        }

        public Task<IReadOnlyCollection<FarmAssignmentReference>>
            GetActiveFarmsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<FarmAssignmentZoneReference>>
            GetActiveZonesAsync(
                Guid tenantId,
                IReadOnlyCollection<Guid> farmIds,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } =
            AccessDecision.Allow();

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Decision);

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

    private sealed class FakeExecutionContext(
        Guid tenantId,
        Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId => tenantId;

        public Guid? ActorId => actorId;

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Guid? MessageId => null;

        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
