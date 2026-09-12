using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Features.RegisterUser;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleCreatesRegistrationAndEmailOutboxAtomically()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new RegisterUserCommand(
                "  OWNER@Example.com ",
                "password123",
                "  Farm Owner  ",
                " 0901234567 ",
                " farm-01 ",
                "  Green Farm  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(fixture.UserRepository.AddedUsers);
        var tenant = Assert.Single(fixture.TenantRepository.AddedTenants);
        var membership = Assert.Single(
            fixture.MembershipRepository.AddedMemberships);
        Assert.Equal("owner@example.com", user.Email);
        Assert.Equal("Farm Owner", user.FullName);
        Assert.Equal("0901234567", user.Phone);
        Assert.Equal("FARM-01", tenant.Code);
        Assert.Equal("Green Farm", tenant.Name);
        Assert.Equal(TenantMemberRole.Owner, membership.Role);
        Assert.Equal(tenant.Id, membership.TenantId);
        Assert.Equal(user.Id, membership.UserId);

        var envelope = Assert.IsType<
            IntegrationEventEnvelope<EmailNotificationRequestedV1>>(
            fixture.Outbox.Envelope);
        Assert.Equal(
            EmailTemplateKeys.TenantRegistrationSuccess,
            envelope.Payload.TemplateKey);
        Assert.Equal(tenant.Id, envelope.TenantId);
        Assert.Equal(user.Id, envelope.ActorId);
        Assert.Equal(Now, envelope.OccurredAt);
        Assert.Equal(
            envelope.Payload.NotificationId.ToString("D"),
            fixture.Outbox.PartitionKey);

        var recipient = Assert.Single(envelope.Payload.Recipients);
        Assert.Equal(user.Email, recipient.Address);
        Assert.Equal(user.FullName, recipient.DisplayName);
        Assert.Equal(
            tenant.Name,
            envelope.Payload.Variables[
                EmailTemplateVariableKeys.TenantName]);
        Assert.Equal(
            tenant.Code,
            envelope.Payload.Variables[
                EmailTemplateVariableKeys.TenantCode]);
        Assert.Equal(
            Now.ToString("O"),
            envelope.Payload.Variables[
                EmailTemplateVariableKeys.RegisteredAt]);
        Assert.Equal(
            "https://app.example.test/login",
            envelope.Payload.Variables[
                EmailTemplateVariableKeys.LoginUrl]);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleDoesNotWriteOutboxWhenEmailAlreadyExists()
    {
        var fixture = CreateFixture();
        fixture.UserRepository.UserByEmail = User.Create(
            "owner@example.com",
            "hash",
            "Existing Owner",
            null,
            UserStatus.Active,
            Now);

        var result = await fixture.Handler.Handle(
            ValidCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("User.EmailAlreadyExists", result.Error.Code);
        Assert.Null(fixture.Outbox.Envelope);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleDoesNotWriteOutboxWhenTenantCodeAlreadyExists()
    {
        var fixture = CreateFixture();
        fixture.TenantRepository.TenantByCode = Tenant.Create(
            "FARM-01",
            "Existing Farm",
            GeneralStatus.Active,
            Now);

        var result = await fixture.Handler.Handle(
            ValidCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.TenantCodeAlreadyExist", result.Error.Code);
        Assert.Null(fixture.Outbox.Envelope);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    private static RegisterUserCommand ValidCommand() =>
        new(
            "owner@example.com",
            "password123",
            "Farm Owner",
            "0901234567",
            "FARM-01",
            "Green Farm");

    private static Fixture CreateFixture()
    {
        var users = new FakeUserRepository();
        var tenants = new FakeTenantRepository();
        var memberships = new FakeTenantMembershipRepository();
        var outbox = new FakeIdentityIntegrationOutbox();
        var unitOfWork = new FakeIdentityUnitOfWork();
        var handler = new RegisterUserCommandHandler(
            users,
            new FakePasswordService(),
            tenants,
            memberships,
            outbox,
            new FakeExecutionContext(Guid.NewGuid()),
            Options.Create(new TenantRegistrationOptions
            {
                LoginUrl = "https://app.example.test/login"
            }),
            new FixedTimeProvider(Now),
            unitOfWork);

        return new Fixture(
            handler,
            users,
            tenants,
            memberships,
            outbox,
            unitOfWork);
    }

    private sealed record Fixture(
        RegisterUserCommandHandler Handler,
        FakeUserRepository UserRepository,
        FakeTenantRepository TenantRepository,
        FakeTenantMembershipRepository MembershipRepository,
        FakeIdentityIntegrationOutbox Outbox,
        FakeIdentityUnitOfWork UnitOfWork);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? UserByEmail { get; set; }

        public List<User> AddedUsers { get; } = [];

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

        public void Add(User user) => AddedUsers.Add(user);

        public void Update(User user)
        {
        }
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public Tenant? TenantByCode { get; set; }

        public List<Tenant> AddedTenants { get; } = [];

        public Task<Tenant?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(null);

        public Task<Tenant?> GetByIdIgnoreStatusAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(null);

        public Task<Tenant?> GetByCodeAsync(
            string tenantCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(TenantByCode);

        public void Add(Tenant tenant) => AddedTenants.Add(tenant);
    }

    private sealed class FakeTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public List<TenantMembership> AddedMemberships { get; } = [];

        public void Add(TenantMembership tenantMembership) =>
            AddedMemberships.Add(tenantMembership);

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
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantMembership?>(null);
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public bool VerifyPassword(
            string password,
            string hashedPassword) => false;

        public string HashPassword(string password) => "PASSWORD_HASH";
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
            return await operation(cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeExecutionContext(Guid correlationId)
        : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId => null;

        public Guid? ActorId => null;

        public Guid CorrelationId => correlationId;

        public Guid? MessageId => null;

        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
