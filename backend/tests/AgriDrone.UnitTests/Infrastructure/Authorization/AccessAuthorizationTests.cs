using System.Security.Claims;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Authorization;

public sealed class AccessAuthorizationTests
{
    [Fact]
    public async Task TenantOwnerPolicyUsesEffectiveTenantAccess()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var provider = CreateProvider(actorId, tenantId);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var accessService = provider.GetRequiredService<FakeEffectiveAccessService>();

        var result = await authorization.AuthorizeAsync(
            CreateAuthenticatedUser(),
            resource: null,
            AccessAuthorizationPolicies.TenantOwner);

        Assert.True(result.Succeeded);
        Assert.Equal(
            (actorId, tenantId, TenantAccessLevel.Owner),
            accessService.LastTenantCheck);
    }

    [Fact]
    public async Task FarmReadPolicyUsesFarmResourceAndMemberAccess()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        using var provider = CreateProvider(actorId, tenantId);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var accessService = provider.GetRequiredService<FakeEffectiveAccessService>();

        var result = await authorization.AuthorizeAsync(
            CreateAuthenticatedUser(),
            new FarmAccessTarget(tenantId, farmId),
            AccessAuthorizationPolicies.FarmRead);

        Assert.True(result.Succeeded);
        Assert.Equal(
            (actorId, tenantId, farmId, FarmAccessLevel.Member),
            accessService.LastFarmCheck);
    }

    [Fact]
    public async Task ZoneManagePolicyUsesZoneResourceAndManagerAccess()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        using var provider = CreateProvider(actorId, tenantId);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var accessService = provider.GetRequiredService<FakeEffectiveAccessService>();

        var result = await authorization.AuthorizeAsync(
            CreateAuthenticatedUser(),
            new ZoneAccessTarget(tenantId, farmId, zoneId),
            AccessAuthorizationPolicies.ZoneManage);

        Assert.True(result.Succeeded);
        Assert.Equal(
            (actorId, tenantId, farmId, zoneId, FarmAccessLevel.Manager),
            accessService.LastZoneCheck);
    }

    [Fact]
    public async Task ResourceFromAnotherTenantIsDeniedBeforeDatabaseCheck()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        using var provider = CreateProvider(actorId, tenantId);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var accessService = provider.GetRequiredService<FakeEffectiveAccessService>();

        var result = await authorization.AuthorizeAsync(
            CreateAuthenticatedUser(),
            new FarmAccessTarget(Guid.NewGuid(), Guid.NewGuid()),
            AccessAuthorizationPolicies.FarmRead);

        Assert.False(result.Succeeded);
        Assert.Null(accessService.LastFarmCheck);
    }

    private static ServiceProvider CreateProvider(
        Guid actorId,
        Guid tenantId)
    {
        var services = new ServiceCollection();
        var accessService = new FakeEffectiveAccessService();

        services.AddLogging();
        services.AddSingleton<IExecutionContext>(
            new FakeExecutionContext(actorId, tenantId));
        services.AddSingleton(accessService);
        services.AddSingleton<IEffectiveAccessService>(accessService);
        services.AddAccessAuthorization();

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreateAuthenticatedUser() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            authenticationType: "Test"));

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public (
            Guid ActorId,
            Guid TenantId,
            TenantAccessLevel RequiredAccess)? LastTenantCheck { get; private set; }

        public (
            Guid ActorId,
            Guid TenantId,
            Guid FarmId,
            FarmAccessLevel RequiredAccess)? LastFarmCheck { get; private set; }

        public (
            Guid ActorId,
            Guid TenantId,
            Guid FarmId,
            Guid ZoneId,
            FarmAccessLevel RequiredAccess)? LastZoneCheck { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            LastTenantCheck = (actorId, tenantId, requiredAccess);
            return Task.FromResult(AccessDecision.Allow());
        }

        public Task<AccessDecision> CheckFarmAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            LastFarmCheck = (actorId, tenantId, farmId, requiredAccess);
            return Task.FromResult(AccessDecision.Allow());
        }

        public Task<AccessDecision> CheckZoneAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            LastZoneCheck = (
                actorId,
                tenantId,
                farmId,
                zoneId,
                requiredAccess);
            return Task.FromResult(AccessDecision.Allow());
        }
    }

    private sealed class FakeExecutionContext(
        Guid actorId,
        Guid tenantId) : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId { get; } = tenantId;

        public Guid? ActorId { get; } = actorId;

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Guid? MessageId => null;

        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
