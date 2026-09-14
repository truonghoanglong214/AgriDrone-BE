using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;
using AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class GetMyFarmAssignmentsQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsMultipleManagerFarmsWithTheirVisibleZones()
    {
        var fixture = CreateFixture();
        var farmOneZone = Guid.NewGuid();
        var selectedFarmTwoZone = Guid.NewGuid();
        var unassignedFarmTwoZone = Guid.NewGuid();

        fixture.FarmReferenceQuery.ActiveFarms =
        [
            new(
                fixture.FarmOneId,
                "FARM-001",
                "North Farm",
                "North address",
                10m),
            new(
                fixture.FarmTwoId,
                "FARM-002",
                "South Farm",
                "South address",
                20m)
        ];
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmOneId, farmOneZone, "Z-01", "North Zone", 5m),
            new(
                fixture.FarmTwoId,
                selectedFarmTwoZone,
                "Z-02",
                "Selected South Zone",
                6m),
            new(
                fixture.FarmTwoId,
                unassignedFarmTwoZone,
                "Z-03",
                "Unassigned South Zone",
                7m)
        ];
        fixture.Queries.Response = new(
        [
            CreateReadModel(
                fixture.FarmOneId,
                FarmAccessScope.AllZones,
                []),
            CreateReadModel(
                fixture.FarmTwoId,
                FarmAccessScope.SelectedZones,
                [selectedFarmTwoZone])
        ],
        1,
        20,
        2);

        var result = await fixture.Handler.Handle(
            new GetMyFarmAssignmentsQuery(FarmMemberRole.Manager, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);

        var first = result.Value.Items[0];
        Assert.Equal("North Farm", first.Farm.Name);
        Assert.Equal(fixture.FarmOneId, first.Farm.Id);
        Assert.Equal([farmOneZone], first.Zones.Select(zone => zone.Id));

        var second = result.Value.Items[1];
        Assert.Equal("South Farm", second.Farm.Name);
        Assert.Equal(
            [selectedFarmTwoZone],
            second.Zones.Select(zone => zone.Id));
        Assert.DoesNotContain(
            second.Zones,
            zone => zone.Id == unassignedFarmTwoZone);

        Assert.Equal(fixture.TenantId, fixture.Queries.TenantId);
        Assert.Equal(fixture.ActorId, fixture.Queries.UserId);
        Assert.Equal(FarmMemberRole.Manager, fixture.Queries.Role);
        Assert.Equal(
            [fixture.FarmOneId, fixture.FarmTwoId],
            fixture.FarmReferenceQuery.RequestedZoneFarmIds);
    }

    [Fact]
    public async Task HandleExcludesArchivedFarmIdsBeforePagination()
    {
        var fixture = CreateFixture();
        var archivedFarmId = Guid.NewGuid();
        fixture.FarmReferenceQuery.ActiveFarms =
        [
            new(fixture.FarmOneId, "FARM-001", "Active Farm", null, 10m)
        ];

        var result = await fixture.Handler.Handle(
            new GetMyFarmAssignmentsQuery(null, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([fixture.FarmOneId], fixture.Queries.ActiveFarmIds);
        Assert.DoesNotContain(archivedFarmId, fixture.Queries.ActiveFarmIds!);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutCurrentTenantAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantMembershipInactive);

        var result = await fixture.Handler.Handle(
            new GetMyFarmAssignmentsQuery(null, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.FarmReferenceQuery.ActiveFarmCallCount);
        Assert.Equal(0, fixture.Queries.CallCount);
    }

    [Theory]
    [InlineData(true, false, "Tenant.ContextRequired")]
    [InlineData(false, true, "User.ContextRequired")]
    public async Task HandleRequiresTenantAndUserContext(
        bool omitTenant,
        bool omitActor,
        string expectedErrorCode)
    {
        var fixture = CreateFixture(omitTenant, omitActor);

        var result = await fixture.Handler.Handle(
            new GetMyFarmAssignmentsQuery(null, 1, 20),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(0, fixture.Queries.CallCount);
    }

    private static MyFarmAssignmentReadModel CreateReadModel(
        Guid farmId,
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> assignedZoneIds) =>
        new(
            Guid.NewGuid(),
            farmId,
            FarmMemberRole.Manager,
            accessScope,
            assignedZoneIds,
            GeneralStatus.Active,
            1,
            DateTimeOffset.UtcNow);

    private static Fixture CreateFixture(
        bool omitTenant = false,
        bool omitActor = false)
    {
        Guid? tenantId = omitTenant ? null : Guid.NewGuid();
        Guid? actorId = omitActor ? null : Guid.NewGuid();
        var farmOneId = Guid.NewGuid();
        var farmTwoId = Guid.NewGuid();
        var queries = new FakeFarmMembershipQueries();
        var farmReferenceQuery = new FakeFarmAssignmentReferenceQuery
        {
            ActiveFarms =
            [
                new(farmOneId, "FARM-001", "Farm One", null, 10m),
                new(farmTwoId, "FARM-002", "Farm Two", null, 20m)
            ]
        };
        var accessService = new FakeEffectiveAccessService();
        var handler = new GetMyFarmAssignmentsQueryHandler(
            queries,
            farmReferenceQuery,
            new FakeExecutionContext(tenantId, actorId),
            accessService);

        return new Fixture(
            handler,
            queries,
            farmReferenceQuery,
            accessService,
            tenantId ?? Guid.Empty,
            actorId ?? Guid.Empty,
            farmOneId,
            farmTwoId);
    }

    private sealed record Fixture(
        GetMyFarmAssignmentsQueryHandler Handler,
        FakeFarmMembershipQueries Queries,
        FakeFarmAssignmentReferenceQuery FarmReferenceQuery,
        FakeEffectiveAccessService AccessService,
        Guid TenantId,
        Guid ActorId,
        Guid FarmOneId,
        Guid FarmTwoId);

    private sealed class FakeFarmMembershipQueries : IFarmMembershipQueries
    {
        public PagedResult<MyFarmAssignmentReadModel> Response { get; set; } =
            new([], 1, 20, 0);

        public int CallCount { get; private set; }

        public Guid? TenantId { get; private set; }

        public Guid? UserId { get; private set; }

        public IReadOnlyCollection<Guid>? ActiveFarmIds { get; private set; }

        public FarmMemberRole? Role { get; private set; }

        public Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
            Guid tenantId,
            Guid farmId,
            Guid userId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<FarmMemberListItemResponse>> GetMembersPageAsync(
            Guid tenantId,
            Guid farmId,
            FarmMemberRole? role,
            GeneralStatus? status,
            PagedRequest pagedRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<MyFarmAssignmentReadModel>>
            GetMyAssignmentsPageAsync(
                Guid tenantId,
                Guid userId,
                IReadOnlyCollection<Guid> activeFarmIds,
                FarmMemberRole? role,
                PagedRequest pagedRequest,
                CancellationToken cancellationToken)
        {
            CallCount++;
            TenantId = tenantId;
            UserId = userId;
            ActiveFarmIds = activeFarmIds;
            Role = role;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeFarmAssignmentReferenceQuery
        : IFarmAssignmentReferenceQuery
    {
        public IReadOnlyCollection<FarmAssignmentReference> ActiveFarms
        {
            get;
            set;
        } = [];

        public IReadOnlyCollection<FarmAssignmentZoneReference> ActiveZones
        {
            get;
            set;
        } = [];

        public int ActiveFarmCallCount { get; private set; }

        public IReadOnlyCollection<Guid>? RequestedZoneFarmIds { get; private set; }

        public Task<bool> IsActiveFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<FarmAssignmentReference>>
            GetActiveFarmsAsync(
                Guid tenantId,
                CancellationToken cancellationToken = default)
        {
            ActiveFarmCallCount++;
            return Task.FromResult(ActiveFarms);
        }

        public Task<IReadOnlyCollection<FarmAssignmentZoneReference>>
            GetActiveZonesAsync(
                Guid tenantId,
                IReadOnlyCollection<Guid> farmIds,
                CancellationToken cancellationToken = default)
        {
            RequestedZoneFarmIds = farmIds;
            return Task.FromResult(ActiveZones);
        }
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
        Guid? tenantId,
        Guid? actorId) : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId => tenantId;

        public Guid? ActorId => actorId;

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Guid? MessageId => null;

        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
