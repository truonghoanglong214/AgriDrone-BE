using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Features.RevokeFarmMemberAssignment;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class RevokeFarmMemberAssignmentCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAllowsTenantAdminToRevokeMemberAssignment()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Inactive, fixture.Assignment.Status);
        Assert.Equal(2, fixture.Assignment.Version);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal(
            "REVOKE_FARM_MEMBER_ASSIGNMENT",
            fixture.AuditWriter.Action);
        Assert.Equal("Active", fixture.AuditWriter.OldStatus);
        Assert.Equal("Inactive", fixture.AuditWriter.NewStatus);
        Assert.Equal("Access no longer required", fixture.AuditWriter.Reason);
    }

    [Fact]
    public async Task HandleRejectsTenantAdminRevokingTenantAdminAssignment()
    {
        var fixture = CreateFixture(TenantMemberRole.TenantAdmin);
        fixture.AccessService.OwnerDecision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(GeneralStatus.Active, fixture.Assignment.Status);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAllowsOwnerToRevokeTenantAdminAssignment()
    {
        var fixture = CreateFixture(TenantMemberRole.TenantAdmin);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Inactive, fixture.Assignment.Status);
        Assert.Equal(2, fixture.Assignment.Version);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsStaleExpectedVersion()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture, expectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.ConcurrentUpdate", result.Error.Code);
        Assert.Equal(GeneralStatus.Active, fixture.Assignment.Status);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleIsIdempotentWhenAssignmentIsAlreadyInactive()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);
        fixture.Assignment.Deactivate(Now.AddMinutes(-1));

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture, expectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Inactive, fixture.Assignment.Status);
        Assert.Equal(2, fixture.Assignment.Version);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
        Assert.Null(fixture.AuditWriter.Action);
    }

    [Fact]
    public async Task HandleRejectsAssignmentOutsideCurrentTenant()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);
        fixture.FarmRepository.Membership = null;

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.NotFound", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleMapsDatabaseConcurrencyConflict()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);
        fixture.UnitOfWork.SaveException = new DbUpdateConcurrencyException();

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmMembership.ConcurrentUpdate", result.Error.Code);
    }

    private static RevokeFarmMemberAssignmentCommand CreateCommand(
        Fixture fixture,
        long expectedVersion = 1) =>
        new(
            fixture.FarmId,
            fixture.TargetUserId,
            expectedVersion,
            "Access no longer required");

    private static Fixture CreateFixture(TenantMemberRole targetRole)
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var assignment = FarmMembership.Create(
            tenantId,
            farmId,
            targetUserId,
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now.AddDays(-1));
        var tenantMembership = TenantMembership.Create(
            tenantId,
            targetUserId,
            targetRole,
            GeneralStatus.Active,
            Now.AddDays(-2),
            Now.AddDays(-2));
        var tenantRepository = new FakeTenantMembershipRepository
        {
            Membership = tenantMembership
        };
        var farmRepository = new FakeFarmMembershipRepository
        {
            Membership = assignment
        };
        var unitOfWork = new FakeIdentityUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var accessService = new FakeEffectiveAccessService();
        var handler = new RevokeFarmMemberAssignmentCommandHandler(
            tenantRepository,
            farmRepository,
            unitOfWork,
            auditWriter,
            new FakeAuditLogSink(),
            new FakeExecutionContext(tenantId, actorId),
            accessService,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            assignment,
            farmRepository,
            unitOfWork,
            auditWriter,
            accessService,
            farmId,
            targetUserId);
    }

    private sealed record Fixture(
        RevokeFarmMemberAssignmentCommandHandler Handler,
        FarmMembership Assignment,
        FakeFarmMembershipRepository FarmRepository,
        FakeIdentityUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeEffectiveAccessService AccessService,
        Guid FarmId,
        Guid TargetUserId);

    private sealed class FakeFarmMembershipRepository
        : IFarmMembershipRepository
    {
        public FarmMembership? Membership { get; set; }

        public void Add(FarmMembership membership) =>
            throw new NotSupportedException();

        public Task<FarmMembership?> GetByFarmAndUserAsync(
            Guid tenantId,
            Guid farmId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Membership);
    }

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public TenantMembership? Membership { get; set; }

        public void Add(TenantMembership tenantMembership) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<TenantMembership>>
            GetActiveByUserIdAsync(
                Guid userId,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantMembership?> GetActiveByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantMembership?> GetByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Membership);

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantMembership?> GetActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision AdminDecision { get; set; } =
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
                    : AdminDecision);

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

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public string? Action { get; private set; }

        public string? OldStatus { get; private set; }

        public string? NewStatus { get; private set; }

        public string? Reason { get; private set; }

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
            OldStatus = oldData?.RootElement.GetProperty("Status").GetString();
            NewStatus = newData?.RootElement.GetProperty("Status").GetString();
            Reason = newData?.RootElement.GetProperty(nameof(Reason)).GetString();
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
