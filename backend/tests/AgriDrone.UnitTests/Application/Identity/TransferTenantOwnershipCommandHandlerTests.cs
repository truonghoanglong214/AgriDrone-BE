using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.TransferTenantOwnership;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class TransferTenantOwnershipCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleTransfersOwnershipAndWritesTwoAudits()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TenantMemberRole.TenantAdmin,
            fixture.CurrentOwner.Role);
        Assert.Equal(TenantMemberRole.Owner, fixture.NewOwner.Role);
        Assert.Equal(2, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal(2, fixture.AuditWriter.Entries.Count);
        Assert.Contains(
            fixture.AuditWriter.Entries,
            entry =>
                entry.EntityId == fixture.CurrentOwner.Id &&
                entry.OldRole == "Owner" &&
                entry.NewRole == "TenantAdmin");
        Assert.Contains(
            fixture.AuditWriter.Entries,
            entry =>
                entry.EntityId == fixture.NewOwner.Id &&
                entry.OldRole == "Member" &&
                entry.NewRole == "Owner");
        Assert.All(
            fixture.AuditWriter.Entries,
            entry => Assert.Equal("TRANSFER_OWNERSHIP", entry.Action));
    }

    [Fact]
    public async Task HandleRejectsTransferToSelf()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.CurrentOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantOwnership.TransferToSelf", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutOwnerAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsWhenDatabaseOwnerIsNotActor()
    {
        var fixture = CreateFixture();
        fixture.Repository.CurrentOwner = CreateMembership(
            fixture.TenantId,
            TenantMemberRole.Owner,
            GeneralStatus.Active,
            UserStatus.Active);

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantOwnership.ConcurrentTransfer",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsInactiveNewOwnerMembership()
    {
        var fixture = CreateFixture(
            newOwnerMembershipStatus: GeneralStatus.Inactive);

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantOwnership.NewOwnerInactive", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsInactiveNewOwnerUser()
    {
        var fixture = CreateFixture(
            newOwnerUserStatus: UserStatus.Inactive);

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantOwnership.NewOwnerUserInactive",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleMapsOptimisticConcurrencyConflict()
    {
        var fixture = CreateFixture();
        fixture.UnitOfWork.ExceptionOnSaveNumber = 2;

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantOwnership.ConcurrentTransfer",
            result.Error.Code);
    }

    [Fact]
    public async Task HandleMapsActiveOwnerUniqueConflict()
    {
        var fixture = CreateFixture();
        fixture.UnitOfWork.TransactionException =
            new ActiveTenantOwnerConflictException(
                new InvalidOperationException("unique constraint"));

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantOwnership.ConcurrentTransfer",
            result.Error.Code);
    }

    [Fact]
    public async Task HandleRejectsMissingNewOwnerMembership()
    {
        var fixture = CreateFixture();
        fixture.Repository.NewOwner = null;

        var result = await fixture.Handler.Handle(
            new TransferTenantOwnershipCommand(
                fixture.NewOwner.UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantOwnership.NewOwnerNotFound", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    private static Fixture CreateFixture(
        GeneralStatus newOwnerMembershipStatus = GeneralStatus.Active,
        UserStatus newOwnerUserStatus = UserStatus.Active)
    {
        var tenantId = Guid.NewGuid();
        var currentOwner = CreateMembership(
            tenantId,
            TenantMemberRole.Owner,
            GeneralStatus.Active,
            UserStatus.Active);
        var newOwner = CreateMembership(
            tenantId,
            TenantMemberRole.Member,
            newOwnerMembershipStatus,
            newOwnerUserStatus);
        var repository = new FakeTenantMembershipRepository
        {
            CurrentOwner = currentOwner,
            NewOwner = newOwner
        };
        var unitOfWork = new FakeIdentityUnitOfWork();
        var accessService = new FakeEffectiveAccessService();
        var auditWriter = new FakeAuditWriter();
        var executionContext = new FakeExecutionContext(
            tenantId,
            currentOwner.UserId,
            Guid.NewGuid());
        var handler = new TransferTenantOwnershipCommandHandler(
            repository,
            unitOfWork,
            accessService,
            auditWriter,
            new FakeAuditLogSink(),
            executionContext,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            repository,
            unitOfWork,
            accessService,
            auditWriter,
            tenantId,
            currentOwner,
            newOwner);
    }

    private static TenantMembership CreateMembership(
        Guid tenantId,
        TenantMemberRole role,
        GeneralStatus membershipStatus,
        UserStatus userStatus)
    {
        var user = User.Create(
            $"{Guid.NewGuid():N}@example.com",
            "hash",
            "User",
            null,
            userStatus,
            Now);
        var membership = TenantMembership.Create(
            tenantId,
            user.Id,
            role,
            membershipStatus,
            Now,
            Now);

        typeof(TenantMembership)
            .GetProperty(nameof(TenantMembership.User))!
            .SetValue(membership, user);

        return membership;
    }

    private sealed record Fixture(
        TransferTenantOwnershipCommandHandler Handler,
        FakeTenantMembershipRepository Repository,
        FakeIdentityUnitOfWork UnitOfWork,
        FakeEffectiveAccessService AccessService,
        FakeAuditWriter AuditWriter,
        Guid TenantId,
        TenantMembership CurrentOwner,
        TenantMembership NewOwner);

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public TenantMembership? CurrentOwner { get; set; }

        public TenantMembership? NewOwner { get; set; }

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
            Task.FromResult(CurrentOwner is not null);

        public Task<TenantMembership?> GetByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                NewOwner?.UserId == userId &&
                NewOwner.TenantId == tenantId
                    ? NewOwner
                    : null);

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                CurrentOwner?.TenantId == tenantId
                    ? CurrentOwner
                    : null);
    }

    private sealed class FakeIdentityUnitOfWork : IIdentityUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public int TransactionCount { get; private set; }

        public int? ExceptionOnSaveNumber { get; set; }

        public Exception? TransactionException { get; set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            if (ExceptionOnSaveNumber == SaveChangesCount)
            {
                throw new DbUpdateConcurrencyException();
            }

            return Task.FromResult(1);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCount++;

            if (TransactionException is not null)
            {
                throw TransactionException;
            }

            return await operation(cancellationToken);
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

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
            Entries.Add(new AuditEntry(
                entityId,
                action,
                oldData?.RootElement.GetProperty("Role").GetString(),
                newData?.RootElement.GetProperty("Role").GetString()));
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

    private sealed record AuditEntry(
        Guid EntityId,
        string Action,
        string? OldRole,
        string? NewRole);

    private sealed class FakeAuditLogSink : IAuditLogSink
    {
        public void AddAuditLog(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeEffectiveAccessService
        : IEffectiveAccessService
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
