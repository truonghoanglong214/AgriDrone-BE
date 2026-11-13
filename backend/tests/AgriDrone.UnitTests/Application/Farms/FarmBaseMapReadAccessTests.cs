using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class FarmBaseMapReadAccessTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailRejectsOwnerDraftOrForeignFarmBeforeReadingMedia(bool ownerDraft)
    {
        await using var context = CreateDatabase();
        var map = FarmBaseMapVersion.PrepareDraft(
            new FarmBaseMapPublicationContext(Guid.NewGuid(), TenantId, FarmId, 1, sourceMissionId: Guid.NewGuid()),
            DateTimeOffset.UtcNow);
        if (!ownerDraft) map.Publish(ActorId, DateTimeOffset.UtcNow);
        context.FarmBaseMapVersions.Add(map);
        await context.SaveChangesAsync();
        var access = new FarmBaseMapReadAccess(new Context(TenantId, ActorId),
            new EffectiveAccess(ownerDraft), new ManagerAccess());
        var preview = new UnusedPreview();
        var handler = new GetFarmBaseMapDetailQueryHandler(context, access, preview);

        var result = await handler.Handle(new GetFarmBaseMapDetailQuery(FarmId, map.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmBaseMapVersion.NotFound", result.Error.Code);
        Assert.Equal(0, preview.Calls);
    }

    [Fact]
    public async Task OwnerCannotListDraftEvenWhenExplicitlyRequested()
    {
        await using var context = CreateDatabase();
        var farm = Farm.Create(TenantId, "TEST", "Test Farm", null, null, null, null,
            GeneralStatus.Active, ActorId, DateTimeOffset.UtcNow);
        context.Farms.Add(farm);
        context.FarmBaseMapVersions.Add(FarmBaseMapVersion.PrepareDraft(
            new FarmBaseMapPublicationContext(Guid.NewGuid(), TenantId, farm.Id, 1), DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        var access = new FarmBaseMapReadAccess(new Context(TenantId, ActorId), new EffectiveAccess(true), new ManagerAccess());
        var preview = new UnusedPreview();

        var result = await new GetFarmBaseMapsQueryHandler(context, access, preview)
            .Handle(new GetFarmBaseMapsQuery(farm.Id, IncludeDraft: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, preview.Calls);
    }

    private static FarmsDbContext CreateDatabase()
    {
        // Only these scalar fields are needed before the handlers reject access.
        // Production PostGIS/JSON mappings are exercised by the relational smoke test.
        using var conventionsContext = new DbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var model = new ModelBuilder(ConventionSet.CreateConventionSet(conventionsContext));
        model.Entity<Farm>().Ignore(farm => farm.Zones);
        model.Entity<Farm>().Ignore(farm => farm.Boundary);
        model.Entity<Farm>().Ignore(farm => farm.CenterPoint);
        model.Entity<FarmBaseMapVersion>().Ignore(map => map.ZoneMapVersions);
        model.Entity<Farm>().HasKey(farm => farm.Id);
        model.Entity<Farm>().Property(farm => farm.TenantId);
        model.Entity<FarmBaseMapVersion>().HasKey(map => map.Id);
        model.Entity<FarmBaseMapVersion>().Property(map => map.TenantId);
        model.Entity<FarmBaseMapVersion>().Property(map => map.FarmId);
        model.Entity<FarmBaseMapVersion>().Property(map => map.Status);
        model.Entity<FarmBaseMapVersion>().Property(map => map.VersionNumber);
        return new FarmsDbContext(new DbContextOptionsBuilder<FarmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).UseModel(model.FinalizeModel()).Options);
    }

    private sealed class UnusedPreview : IFarmBaseMapPreviewReader
    {
        public int Calls { get; private set; }
        public Task<FarmBaseMapPreview?> GetAsync(Guid tenantId, Guid farmId, Guid missionId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Unauthorized requests must not sign media.");
        }
    }

    [Fact]
    public async Task OwnerWithFarmAccessCanReadReleasedMapsButNotDrafts()
    {
        var effective = new EffectiveAccess(true);
        var service = new FarmBaseMapReadAccess(new Context(TenantId, ActorId), effective, new ManagerAccess());

        var result = await service.CheckAsync(TenantId, FarmId, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.False(result.CanReadDraft);
        Assert.Equal((ActorId, TenantId, FarmId, FarmAccessLevel.Member), effective.LastCheck);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MissingActorWrongTenantOrDeniedFarmCannotReceivePreview(bool actorPresent, bool wrongTenant)
    {
        var effective = new EffectiveAccess(false);
        var context = new Context(wrongTenant ? Guid.NewGuid() : TenantId, actorPresent ? ActorId : null);
        var service = new FarmBaseMapReadAccess(context, effective, new ManagerAccess());

        var result = await service.CheckAsync(TenantId, FarmId, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.CanReadDraft);
        if (!actorPresent || wrongTenant) Assert.Null(effective.LastCheck);
    }

    [Fact]
    public async Task AssignedSystemManagerDoesNotNeedTenantMembershipAndCanReadDrafts()
    {
        var effective = new EffectiveAccess(false);
        var service = new FarmBaseMapReadAccess(new Context(null, ActorId), effective,
            new ManagerAccess(SystemManagerFarmAccess.Allowed(TenantId, FarmId, Guid.NewGuid())));

        var result = await service.CheckAsync(TenantId, FarmId, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.True(result.CanReadDraft);
        Assert.Null(effective.LastCheck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagerGrantMustMatchBothFarmAndTenant(bool wrongTenant)
    {
        var service = new FarmBaseMapReadAccess(new Context(null, ActorId), new EffectiveAccess(true),
            new ManagerAccess(SystemManagerFarmAccess.Allowed(
                wrongTenant ? Guid.NewGuid() : TenantId, wrongTenant ? FarmId : Guid.NewGuid(), Guid.NewGuid())));

        var result = await service.CheckAsync(TenantId, FarmId, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.CanReadDraft);
    }

    private sealed class Context(Guid? tenantId, Guid? actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId => actorId;
        public Guid CorrelationId => Guid.Empty;
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class ManagerAccess(SystemManagerFarmAccess? grant = null) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(grant ?? SystemManagerFarmAccess.Denied("Not assigned"));
    }

    private sealed class EffectiveAccess(bool allowed) : IEffectiveAccessService
    {
        public (Guid Actor, Guid Tenant, Guid Farm, FarmAccessLevel Level)? LastCheck { get; private set; }

        public Task<AccessDecision> CheckFarmAsync(Guid actorId, Guid tenantId, Guid farmId,
            FarmAccessLevel requiredAccess, CancellationToken cancellationToken = default)
        {
            LastCheck = (actorId, tenantId, farmId, requiredAccess);
            return Task.FromResult(allowed ? AccessDecision.Allow() : AccessDecision.Deny(AccessDenialReason.TenantMembershipNotFound));
        }

        public Task<AccessDecision> CheckTenantAsync(Guid actorId, Guid tenantId,
            TenantAccessLevel requiredAccess, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AccessDecision> CheckZoneAsync(Guid actorId, Guid tenantId, Guid farmId, Guid zoneId,
            FarmAccessLevel requiredAccess, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
