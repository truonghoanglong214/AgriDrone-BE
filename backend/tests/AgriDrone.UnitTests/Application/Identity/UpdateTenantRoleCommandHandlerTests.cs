using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class UpdateTenantRoleCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandlePromotesMemberAndWritesAudit()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.TenantAdmin),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TenantMemberRole.TenantAdmin,
            fixture.Repository.Membership!.Role);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal("Member", fixture.AuditWriter.OldRole);
        Assert.Equal("TenantAdmin", fixture.AuditWriter.NewRole);
        Assert.Equal("UPDATE_ROLE", fixture.AuditWriter.Action);
        Assert.Equal(fixture.TenantId, fixture.Repository.RequestedTenantId);
        Assert.Equal(fixture.TargetUserId, fixture.Repository.RequestedUserId);
    }

    [Fact]
    public async Task HandleRejectsChangingOwnerRole()
    {
        var fixture = CreateFixture(TenantMemberRole.Owner);

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.Member),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantMembership.OwnerRoleProtected",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
        Assert.Null(fixture.AuditWriter.Action);
    }

    [Fact]
    public async Task HandleRejectsChangingActorsOwnRole()
    {
        var fixture = CreateFixture(
            TenantMemberRole.Member,
            targetIsActor: true);

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.TenantAdmin),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantMembership.SelfRoleChangeForbidden",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutOwnerAccess()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.TenantAdmin),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleIsNoOpWhenRoleIsUnchanged()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
        Assert.Null(fixture.AuditWriter.Action);
    }

    [Fact]
    public async Task HandleMapsOptimisticConcurrencyConflict()
    {
        var fixture = CreateFixture(TenantMemberRole.Member);
        fixture.UnitOfWork.SaveException =
            new DbUpdateConcurrencyException();

        var result = await fixture.Handler.Handle(
            new UpdateTenantRoleCommand(
                fixture.TargetUserId,
                TenantMemberRole.TenantAdmin),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantMembership.ConcurrentUpdate",
            result.Error.Code);
    }

    private static Fixture CreateFixture(
        TenantMemberRole role,
        bool targetIsActor = false)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetUserId = targetIsActor ? actorId : Guid.NewGuid();
        var membership = CreateMembership(tenantId, targetUserId, role);
        var repository = new FakeTenantMembershipRepository
        {
            Membership = membership
        };
        var unitOfWork = new FakeIdentityUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var accessService = new FakeEffectiveAccessService();
        var executionContext = new FakeExecutionContext(
            tenantId,
            actorId,
            Guid.NewGuid());

        var handler = new UpdateTenantRoleCommandHandler(
            repository,
            unitOfWork,
            auditWriter,
            new FakeAuditLogSink(),
            executionContext,
            accessService,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            repository,
            unitOfWork,
            auditWriter,
            accessService,
            tenantId,
            targetUserId);
    }

    private static TenantMembership CreateMembership(
        Guid tenantId,
        Guid userId,
        TenantMemberRole role)
    {
        var user = User.Create(
            "member@example.com",
            "hash",
            "Member",
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

        typeof(TenantMembership)
            .GetProperty(nameof(TenantMembership.User))!
            .SetValue(membership, user);

        return membership;
    }

    private sealed record Fixture(
        UpdateTenantRoleCommandHandler Handler,
        FakeTenantMembershipRepository Repository,
        FakeIdentityUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeEffectiveAccessService AccessService,
        Guid TenantId,
        Guid TargetUserId);

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public TenantMembership? Membership { get; set; }

        public Guid? RequestedUserId { get; private set; }

        public Guid? RequestedTenantId { get; private set; }

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
            CancellationToken cancellationToken)
        {
            RequestedUserId = userId;
            RequestedTenantId = tenantId;
            return Task.FromResult(Membership);
        }

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveOwnerAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
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

        public string? OldRole { get; private set; }

        public string? NewRole { get; private set; }

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
            OldRole = oldData?.RootElement.GetProperty("Role").GetString();
            NewRole = newData?.RootElement.GetProperty("Role").GetString();
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
