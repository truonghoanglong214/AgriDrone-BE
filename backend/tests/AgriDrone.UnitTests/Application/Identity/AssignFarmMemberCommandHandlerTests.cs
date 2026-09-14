using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class AssignFarmMemberCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAssignsActiveTenantMemberAsFarmManager()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(fixture.FarmRepository.AddedMembership);
        Assert.Equal(
            FarmMemberRole.Manager,
            fixture.FarmRepository.AddedMembership.Role);
        Assert.Equal(
            FarmAccessScope.AllZones,
            fixture.FarmRepository.AddedMembership.AccessScope);
        Assert.Equal(GeneralStatus.Active, result.Value.Status);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal("ASSIGN_FARM_MEMBER", fixture.AuditWriter.Action);
        Assert.Equal(fixture.FarmId, fixture.AuditWriter.FarmId);
        Assert.Equal("Manager", fixture.AuditWriter.NewRole);
        Assert.Equal("AllZones", fixture.AuditWriter.NewAccessScope);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutTenantAdminAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsFarmOutsideCurrentTenant()
    {
        var fixture = CreateFixture();
        fixture.FarmReferenceQuery.IsActive = false;

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.FarmNotFound", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAlsoAssignsActiveTenantAdminAsFarmManager()
    {
        var fixture = CreateFixture(TenantMemberRole.TenantAdmin);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(fixture.FarmRepository.AddedMembership);
        Assert.Equal(
            FarmMemberRole.Manager,
            fixture.FarmRepository.AddedMembership.Role);
    }

    [Fact]
    public async Task HandleAssignsWorkerToSelectedZones()
    {
        var fixture = CreateFixture();
        var zoneIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        fixture.FarmReferenceQuery.ActiveZones = zoneIds
            .Select((zoneId, index) => new FarmAssignmentZoneReference(
                fixture.FarmId,
                zoneId,
                $"ZONE-{index + 1}",
                $"Zone {index + 1}",
                null))
            .ToArray();

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: zoneIds),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FarmMemberRole.Worker, result.Value.Role);
        Assert.Equal(FarmAccessScope.SelectedZones, result.Value.AccessScope);
        Assert.Equal(zoneIds.Order(), result.Value.ZoneIds);
        Assert.Equal(
            zoneIds.Order(),
            fixture.FarmRepository.Membership!.ZoneAssignments
                .Where(assignment => assignment.RevokedAt is null)
                .Select(assignment => assignment.ZoneId)
                .Order());
    }

    [Fact]
    public async Task HandleUpdatesManagerAllZonesToWorkerSelectedZones()
    {
        var fixture = CreateFixture();
        var zoneId = Guid.NewGuid();
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmId, zoneId, "ZONE-1", "Zone 1", null)
        ];
        fixture.FarmRepository.Membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now.AddDays(-1));

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                expectedVersion: 1,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: [zoneId]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FarmMemberRole.Worker, result.Value.Role);
        Assert.Equal(FarmAccessScope.SelectedZones, result.Value.AccessScope);
        Assert.Equal([zoneId], result.Value.ZoneIds);
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task HandleUpdatesSelectedZoneSetAndIncrementsVersionOnce()
    {
        var fixture = CreateFixture();
        var removedZoneId = Guid.NewGuid();
        var retainedZoneId = Guid.NewGuid();
        var addedZoneId = Guid.NewGuid();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [removedZoneId, retainedZoneId],
            Guid.NewGuid(),
            Now.AddDays(-1));
        fixture.FarmRepository.Membership = membership;
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmId, removedZoneId, "ZONE-1", "Zone 1", null),
            new(fixture.FarmId, retainedZoneId, "ZONE-2", "Zone 2", null),
            new(fixture.FarmId, addedZoneId, "ZONE-3", "Zone 3", null)
        ];

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                expectedVersion: 1,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: [retainedZoneId, addedZoneId]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, membership.Version);
        Assert.Equal(
            new[] { addedZoneId, retainedZoneId }.Order(),
            membership.ZoneAssignments
                .Where(assignment => assignment.RevokedAt is null)
                .Select(assignment => assignment.ZoneId)
                .Order());
        Assert.NotNull(membership.ZoneAssignments.Single(
            assignment => assignment.ZoneId == removedZoneId).RevokedAt);
    }

    [Fact]
    public async Task HandleUpdatesSelectedZonesToAllZonesAndRevokesExplicitZones()
    {
        var fixture = CreateFixture();
        var zoneId = Guid.NewGuid();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [zoneId],
            Guid.NewGuid(),
            Now.AddDays(-1));
        fixture.FarmRepository.Membership = membership;
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmId, zoneId, "ZONE-1", "Zone 1", null)
        ];

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                expectedVersion: 1,
                accessScope: FarmAccessScope.AllZones),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FarmAccessScope.AllZones, membership.AccessScope);
        Assert.DoesNotContain(
            membership.ZoneAssignments,
            assignment => assignment.RevokedAt is null);
        Assert.Empty(result.Value.ZoneIds);
    }

    [Fact]
    public async Task HandleRejectsSelectedZoneOutsideFarm()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: [Guid.NewGuid()]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.InvalidZones", result.Error.Code);
        Assert.Null(fixture.FarmRepository.AddedMembership);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleIsIdempotentWhenSelectedZonesMatchRegardlessOfOrder()
    {
        var fixture = CreateFixture();
        var firstZoneId = Guid.NewGuid();
        var secondZoneId = Guid.NewGuid();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [firstZoneId, secondZoneId],
            Guid.NewGuid(),
            Now.AddDays(-1));
        fixture.FarmRepository.Membership = membership;
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmId, firstZoneId, "ZONE-1", "Zone 1", null),
            new(fixture.FarmId, secondZoneId, "ZONE-2", "Zone 2", null)
        ];

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: [secondZoneId, firstZoneId]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRequiresVersionWhenOnlySelectedZonesChange()
    {
        var fixture = CreateFixture();
        var currentZoneId = Guid.NewGuid();
        var replacementZoneId = Guid.NewGuid();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [currentZoneId],
            Guid.NewGuid(),
            Now.AddDays(-1));
        fixture.FarmRepository.Membership = membership;
        fixture.FarmReferenceQuery.ActiveZones =
        [
            new(fixture.FarmId, currentZoneId, "ZONE-1", "Zone 1", null),
            new(fixture.FarmId, replacementZoneId, "ZONE-2", "Zone 2", null)
        ];

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                role: FarmMemberRole.Worker,
                accessScope: FarmAccessScope.SelectedZones,
                zoneIds: [replacementZoneId]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FarmMembership.ExpectedVersionRequired",
            result.Error.Code);
        Assert.Equal(1, membership.Version);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsTenantAdminActorAssigningTenantAdmin()
    {
        var fixture = CreateFixture(TenantMemberRole.TenantAdmin);
        fixture.AccessService.OwnerDecision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Null(fixture.FarmRepository.AddedMembership);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsWorkerRoleForTenantAdminTarget()
    {
        var fixture = CreateFixture(TenantMemberRole.TenantAdmin);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture, role: FarmMemberRole.Worker),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FarmMembership.TenantAdminMustBeManager",
            result.Error.Code);
        Assert.Null(fixture.FarmRepository.AddedMembership);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsTargetTenantOwner()
    {
        var fixture = CreateFixture(TenantMemberRole.Owner);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FarmMembership.TargetTenantRoleNotAssignable",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleIsIdempotentWhenAssignmentIsAlreadyActive()
    {
        var fixture = CreateFixture();
        fixture.FarmRepository.Membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
        Assert.Null(fixture.AuditWriter.Action);
    }

    [Fact]
    public async Task HandleReactivatesExistingAssignmentWithMatchingVersion()
    {
        var fixture = CreateFixture();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now.AddDays(-2));
        SetProperty(membership, nameof(FarmMembership.Status), GeneralStatus.Inactive);
        SetProperty(membership, nameof(FarmMembership.Version), 7L);
        fixture.FarmRepository.Membership = membership;

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture, expectedVersion: 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Active, membership.Status);
        Assert.Equal(Now, membership.JoinedAt);
        Assert.Equal(8, membership.Version);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRequiresVersionToChangeExistingAssignment()
    {
        var fixture = CreateFixture();
        var membership = FarmMembership.Create(
            fixture.TenantId,
            fixture.FarmId,
            fixture.TargetUserId,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now.AddDays(-2));
        SetProperty(membership, nameof(FarmMembership.Status), GeneralStatus.Inactive);
        fixture.FarmRepository.Membership = membership;

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "FarmMembership.ExpectedVersionRequired",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleMapsDatabaseConcurrencyConflict()
    {
        var fixture = CreateFixture();
        fixture.UnitOfWork.SaveException = new DbUpdateConcurrencyException();

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.ConcurrentUpdate", result.Error.Code);
    }

    private static AssignFarmMemberCommand CreateCommand(
        Fixture fixture,
        long? expectedVersion = null,
        FarmMemberRole role = FarmMemberRole.Manager,
        FarmAccessScope accessScope = FarmAccessScope.AllZones,
        IReadOnlyCollection<Guid>? zoneIds = null) =>
        new(
            fixture.FarmId,
            fixture.TargetUserId,
            role,
            accessScope,
            zoneIds ?? [],
            expectedVersion,
            "Manage the farm");

    private static Fixture CreateFixture(
        TenantMemberRole targetRole = TenantMemberRole.Member)
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var tenantRepository = new FakeTenantMembershipRepository
        {
            Membership = CreateTenantMembership(
                tenantId,
                targetUserId,
                targetRole)
        };
        var farmRepository = new FakeFarmMembershipRepository();
        var farmReferenceQuery = new FakeFarmAssignmentReferenceQuery();
        var unitOfWork = new FakeIdentityUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var accessService = new FakeEffectiveAccessService();
        var executionContext = new FakeExecutionContext(
            tenantId,
            actorId,
            Guid.NewGuid());

        var handler = new AssignFarmMemberCommandHandler(
            tenantRepository,
            farmRepository,
            farmReferenceQuery,
            unitOfWork,
            auditWriter,
            new FakeAuditLogSink(),
            executionContext,
            accessService,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            farmRepository,
            farmReferenceQuery,
            unitOfWork,
            auditWriter,
            accessService,
            tenantId,
            farmId,
            targetUserId);
    }

    private static TenantMembership CreateTenantMembership(
        Guid tenantId,
        Guid userId,
        TenantMemberRole role)
    {
        var user = User.Create(
            "admin@example.com",
            "hash",
            "Tenant Admin",
            null,
            UserStatus.Active,
            Now);
        var membership = TenantMembership.Create(
            tenantId,
            userId,
            role,
            GeneralStatus.Active,
            Now,
            Now);

        SetProperty(membership, nameof(TenantMembership.User), user);
        return membership;
    }

    private static void SetProperty<T>(
        object target,
        string propertyName,
        T value) =>
        target.GetType()
            .GetProperty(propertyName)!
            .SetValue(target, value);

    private sealed record Fixture(
        AssignFarmMemberCommandHandler Handler,
        FakeFarmMembershipRepository FarmRepository,
        FakeFarmAssignmentReferenceQuery FarmReferenceQuery,
        FakeIdentityUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeEffectiveAccessService AccessService,
        Guid TenantId,
        Guid FarmId,
        Guid TargetUserId);

    private sealed class FakeFarmMembershipRepository
        : IFarmMembershipRepository
    {
        public FarmMembership? Membership { get; set; }

        public FarmMembership? AddedMembership { get; private set; }

        public void Add(FarmMembership membership)
        {
            AddedMembership = membership;
            Membership = membership;
        }

        public Task<FarmMembership?> GetByFarmAndUserAsync(
            Guid tenantId,
            Guid farmId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Membership);
    }

    private sealed class FakeFarmAssignmentReferenceQuery
        : IFarmAssignmentReferenceQuery
    {
        public bool IsActive { get; set; } = true;

        public IReadOnlyCollection<FarmAssignmentZoneReference> ActiveZones
        {
            get;
            set;
        } = [];

        public Task<bool> IsActiveFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(IsActive);

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
            Task.FromResult(ActiveZones);
    }

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public TenantMembership? Membership { get; set; }

        public void Add(TenantMembership tenantMembership)
        {
        }

        public Task<IReadOnlyCollection<TenantMembership>>
            GetActiveByUserIdAsync(
                Guid userId,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TenantMembership>>([]);

        public Task<TenantMembership?> GetActiveByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<bool> HasActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<TenantMembership?> GetByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Membership);

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
    }

    private sealed class FakeIdentityUnitOfWork : IIdentityUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Exception? SaveException { get; set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;

            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public string? Action { get; private set; }

        public Guid? FarmId { get; private set; }

        public string? NewRole { get; private set; }

        public string? NewAccessScope { get; private set; }

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
            DateTimeOffset createdAt)
        {
            Action = action;
            FarmId = farmId;
            NewRole = newData?.RootElement.GetProperty("Role").GetString();
            NewAccessScope = newData?.RootElement
                .GetProperty("AccessScope")
                .GetString();
        }

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
            throw new NotSupportedException();
    }

    private sealed class FakeAuditLogSink : IAuditLogSink
    {
        public void AddAuditLog(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } =
            AccessDecision.Allow();

        public AccessDecision OwnerDecision { get; set; } =
            AccessDecision.Allow();

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                requiredAccess == TenantAccessLevel.Owner
                    ? OwnerDecision
                    : Decision);

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
        Guid actorId,
        Guid correlationId) : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId => tenantId;

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
