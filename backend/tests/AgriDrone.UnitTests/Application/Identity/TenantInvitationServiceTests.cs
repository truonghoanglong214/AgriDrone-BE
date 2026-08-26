using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class TenantInvitationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InviteAsyncCreatesNormalizedInvitationAndOutboxMessage()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "  ADMIN@Example.COM ",
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invitation = Assert.Single(
            fixture.InvitationRepository.AddedInvitations);
        Assert.Equal("admin@example.com", invitation.Email);
        Assert.Equal(TenantMemberRole.TenantAdmin, invitation.Role);
        Assert.Equal(TenantInvitationPurpose.Membership, invitation.Purpose);
        Assert.Equal(Now.AddHours(24), invitation.ExpiresAt);
        Assert.Equal(invitation.Id, result.Value.InvitationId);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);

        var envelope = Assert.IsType<
            IntegrationEventEnvelope<TenantInvitationEmailRequestedV1>>(
            fixture.Outbox.Envelope);
        Assert.Equal(invitation.Id, envelope.Payload.InvitationId);
        Assert.Equal(FakeInvitationTokenService.PlainTextToken,
            envelope.Payload.PlainTextToken);
        Assert.Equal(fixture.Tenant.Id, envelope.TenantId);
        Assert.Equal(fixture.InviterId, envelope.ActorId);
        Assert.Equal(invitation.Id.ToString("D"), fixture.Outbox.PartitionKey);
    }

    [Fact]
    public async Task InviteAsyncRejectsInvitingTheCurrentUser()
    {
        var fixture = CreateFixture();
        var inviter = User.Create(
            "owner@example.com",
            "hash",
            "Owner",
            null,
            UserStatus.Active,
            Now);
        fixture.UserRepository.UserByEmail = inviter;

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                inviter.Id,
                inviter.Email,
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantInvitation.InviteSelfNotAllowed",
            result.Error.Code);
        Assert.Empty(fixture.InvitationRepository.AddedInvitations);
        Assert.Null(fixture.Outbox.Envelope);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task InviteAsyncRejectsAnActivePendingInvitation()
    {
        var fixture = CreateFixture();
        fixture.InvitationRepository.PendingInvitation =
            TenantInvitation.Create(
                fixture.Tenant.Id,
                "admin@example.com",
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership,
                "OLD_HASH",
                fixture.InviterId,
                Now.AddHours(1),
                Now.AddHours(-1));

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "admin@example.com",
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantInvitation.AlreadyPending", result.Error.Code);
        Assert.Empty(fixture.InvitationRepository.AddedInvitations);
        Assert.Null(fixture.Outbox.Envelope);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task InviteAsyncExpiresOldPendingInvitationBeforeReplacingIt()
    {
        var fixture = CreateFixture();
        var expiredInvitation = TenantInvitation.Create(
            fixture.Tenant.Id,
            "member@example.com",
            TenantMemberRole.Member,
            TenantInvitationPurpose.Membership,
            "OLD_HASH",
            fixture.InviterId,
            Now.AddMinutes(-1),
            Now.AddHours(-25));
        fixture.InvitationRepository.PendingInvitation = expiredInvitation;

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "member@example.com",
                TenantMemberRole.Member,
                TenantInvitationPurpose.Membership),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TenantInvitationStatus.Expired,
            expiredInvitation.Status);
        Assert.Single(fixture.InvitationRepository.AddedInvitations);
        Assert.Equal(2, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task InviteAsyncMapsConcurrentPendingConflictToAlreadyPending()
    {
        var fixture = CreateFixture();
        fixture.UnitOfWork.TransactionException =
            new PendingTenantInvitationConflictException(
                new InvalidOperationException("unique constraint"));

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "admin@example.com",
                TenantMemberRole.TenantAdmin,
                TenantInvitationPurpose.Membership),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantInvitation.AlreadyPending", result.Error.Code);
    }

    [Fact]
    public async Task InviteAsyncCreatesOwnerProvisioningWhenTenantHasNoOwner()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "owner@example.com",
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invitation = Assert.Single(
            fixture.InvitationRepository.AddedInvitations);
        Assert.Equal(TenantMemberRole.Owner, invitation.Role);
        Assert.Equal(
            TenantInvitationPurpose.OwnerProvisioning,
            invitation.Purpose);
    }

    [Fact]
    public async Task InviteAsyncRejectsOwnerProvisioningWhenOwnerAlreadyExists()
    {
        var fixture = CreateFixture();
        fixture.MembershipRepository.HasActiveOwner = true;

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "owner@example.com",
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantInvitation.OwnerAlreadyAssigned",
            result.Error.Code);
        Assert.Empty(fixture.InvitationRepository.AddedInvitations);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task InviteAsyncRejectsSecondPendingOwnerProvisioning()
    {
        var fixture = CreateFixture();
        fixture.InvitationRepository.PendingOwnerProvisioning =
            TenantInvitation.Create(
                fixture.Tenant.Id,
                "first-owner@example.com",
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning,
                "OLD_HASH",
                fixture.InviterId,
                Now.AddHours(1),
                Now.AddHours(-1));

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "second-owner@example.com",
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantInvitation.OwnerProvisioningAlreadyPending",
            result.Error.Code);
        Assert.Empty(fixture.InvitationRepository.AddedInvitations);
    }

    [Fact]
    public async Task InviteAsyncMapsConcurrentOwnerProvisioningConflict()
    {
        var fixture = CreateFixture();
        fixture.UnitOfWork.TransactionException =
            new PendingTenantOwnerProvisioningConflictException(
                new InvalidOperationException("unique constraint"));

        var result = await fixture.Service.InviteAsync(
            new CreateTenantInvitationRequest(
                fixture.Tenant.Id,
                fixture.InviterId,
                "owner@example.com",
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantInvitation.OwnerProvisioningAlreadyPending",
            result.Error.Code);
    }

    [Fact]
    public void InvitationRejectsPurposeAndRoleMismatch()
    {
        Assert.Throws<ArgumentException>(() => TenantInvitation.Create(
            Guid.NewGuid(),
            "owner@example.com",
            TenantMemberRole.Owner,
            TenantInvitationPurpose.Membership,
            "TOKEN_HASH",
            Guid.NewGuid(),
            Now.AddHours(1),
            Now));
    }

    private static Fixture CreateFixture()
    {
        var tenant = Tenant.Create(
            "TENANT",
            "Tenant",
            GeneralStatus.Active,
            Now);
        var userRepository = new FakeUserRepository();
        var tenantRepository = new FakeTenantRepository(tenant);
        var membershipRepository = new FakeTenantMembershipRepository();
        var invitationRepository = new FakeTenantInvitationRepository();
        var outbox = new FakeIdentityIntegrationOutbox();
        var unitOfWork = new FakeIdentityUnitOfWork();
        var inviterId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var service = new TenantInvitationService(
            userRepository,
            tenantRepository,
            membershipRepository,
            invitationRepository,
            new FakeInvitationTokenService(),
            outbox,
            new FakeExecutionContext(
                tenant.Id,
                inviterId,
                correlationId),
            Options.Create(new TenantInvitationOptions
            {
                AcceptUrl = "https://example.test/invitations/accept",
                ExpirationHours = 24
            }),
            new FixedTimeProvider(Now),
            unitOfWork);

        return new Fixture(
            service,
            tenant,
            inviterId,
            userRepository,
            membershipRepository,
            invitationRepository,
            outbox,
            unitOfWork);
    }

    private sealed record Fixture(
        TenantInvitationService Service,
        Tenant Tenant,
        Guid InviterId,
        FakeUserRepository UserRepository,
        FakeTenantMembershipRepository MembershipRepository,
        FakeTenantInvitationRepository InvitationRepository,
        FakeIdentityIntegrationOutbox Outbox,
        FakeIdentityUnitOfWork UnitOfWork);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? UserByEmail { get; set; }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UserByEmail);

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UserByEmail);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<List<User>>([]);

        public void Add(User user)
        {
        }

        public void Update(User user)
        {
        }
    }

    private sealed class FakeTenantRepository(Tenant tenant)
        : ITenantRepository
    {
        public Task<Tenant?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(id == tenant.Id ? tenant : null);

        public Task<Tenant?> GetByIdIgnoreStatusAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<Tenant?> GetByCodeAsync(
            string tenantCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(null);

        public void Add(Tenant tenantToAdd)
        {
        }
    }

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public bool HasActiveOwner { get; set; }

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
            Task.FromResult(HasActiveOwner);

        public Task<TenantMembership?> GetByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetByIdAsync(Guid membershipId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TenantMembership?> GetActiveOwnerAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeTenantInvitationRepository
        : ITenantInvitationRepository
    {
        public TenantInvitation? PendingInvitation { get; set; }

        public TenantInvitation? PendingOwnerProvisioning { get; set; }

        public List<TenantInvitation> AddedInvitations { get; } = [];

        public Task<TenantInvitation?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> GetPendingAsync(
            Guid tenantId,
            string email,
            CancellationToken cancellationToken) =>
            Task.FromResult(PendingInvitation);

        public Task<TenantInvitation?> GetPendingOwnerProvisioningAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult(PendingOwnerProvisioning);

        public Task<TenantInvitation?> GetByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public void Add(TenantInvitation invitation) =>
            AddedInvitations.Add(invitation);
    }

    private sealed class FakeInvitationTokenService
        : IInvitationTokenService
    {
        public const string PlainTextToken =
            "0123456789ABCDEF0123456789ABCDEF" +
            "0123456789ABCDEF0123456789ABCDEF";

        public InvitationTokenResult Generate() =>
            new(PlainTextToken, "TOKEN_HASH");

        public string Hash(string plainTextToken) => "TOKEN_HASH";
    }

    private sealed class FakeIdentityIntegrationOutbox
        : IIdentityIntegrationOutbox
    {
        public object? Envelope { get; private set; }

        public string? PartitionKey { get; private set; }

        public void Add<TPayload>(
            IntegrationEventEnvelope<TPayload> envelope,
            string? partitionKey = null)
        {
            Envelope = envelope;
            PartitionKey = partitionKey;
        }
    }

    private sealed class FakeIdentityUnitOfWork : IIdentityUnitOfWork
    {
        public Exception? TransactionException { get; set; }

        public int SaveChangesCount { get; private set; }

        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
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

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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

        public ExecutionContextSource Source =>
            ExecutionContextSource.Http;
    }
}
