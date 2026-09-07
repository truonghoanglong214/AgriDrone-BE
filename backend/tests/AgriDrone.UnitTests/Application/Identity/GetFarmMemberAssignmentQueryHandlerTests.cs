using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class GetFarmMemberAssignmentQueryHandlerTests
{
    [Fact]
    public async Task HandleReturnsAssignmentWithCurrentVersionAndZones()
    {
        var fixture = CreateFixture();
        var zoneIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        fixture.Queries.Response = new GetFarmMemberAssignmentResponse(
            Guid.NewGuid(),
            fixture.TenantId,
            fixture.FarmId,
            fixture.UserId,
            FarmMemberRole.Manager,
            FarmAccessScope.SelectedZones,
            zoneIds,
            GeneralStatus.Active,
            4,
            DateTimeOffset.UtcNow);

        var result = await fixture.Handler.Handle(
            new GetFarmMemberAssignmentQuery(
                fixture.FarmId,
                fixture.UserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Version);
        Assert.Equal(zoneIds, result.Value.ZoneIds);
        Assert.Equal(fixture.TenantId, fixture.Queries.TenantId);
        Assert.Equal(fixture.FarmId, fixture.Queries.FarmId);
        Assert.Equal(fixture.UserId, fixture.Queries.UserId);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutTenantAdminAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            new GetFarmMemberAssignmentQuery(
                fixture.FarmId,
                fixture.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.Queries.CallCount);
    }

    [Fact]
    public async Task HandleHidesFarmOutsideCurrentTenant()
    {
        var fixture = CreateFixture();
        fixture.FarmReferenceQuery.IsActive = false;

        var result = await fixture.Handler.Handle(
            new GetFarmMemberAssignmentQuery(
                fixture.FarmId,
                fixture.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.FarmNotFound", result.Error.Code);
        Assert.Equal(0, fixture.Queries.CallCount);
    }

    [Fact]
    public async Task HandleReturnsNotFoundWhenUserHasNoFarmAssignment()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new GetFarmMemberAssignmentQuery(
                fixture.FarmId,
                fixture.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.NotFound", result.Error.Code);
    }

    private static Fixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var queries = new FakeFarmMembershipQueries();
        var farmReferenceQuery = new FakeFarmAssignmentReferenceQuery();
        var accessService = new FakeEffectiveAccessService();
        var handler = new GetFarmMemberAssignmentQueryHandler(
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
            farmId,
            userId);
    }

    private sealed record Fixture(
        GetFarmMemberAssignmentQueryHandler Handler,
        FakeFarmMembershipQueries Queries,
        FakeFarmAssignmentReferenceQuery FarmReferenceQuery,
        FakeEffectiveAccessService AccessService,
        Guid TenantId,
        Guid FarmId,
        Guid UserId);

    private sealed class FakeFarmMembershipQueries : IFarmMembershipQueries
    {
        public GetFarmMemberAssignmentResponse? Response { get; set; }

        public int CallCount { get; private set; }

        public Guid? TenantId { get; private set; }

        public Guid? FarmId { get; private set; }

        public Guid? UserId { get; private set; }

        public Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
            Guid tenantId,
            Guid farmId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            TenantId = tenantId;
            FarmId = farmId;
            UserId = userId;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeFarmAssignmentReferenceQuery
        : IFarmAssignmentReferenceQuery
    {
        public bool IsActive { get; set; } = true;

        public Task<bool> IsActiveFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IsActive);
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
