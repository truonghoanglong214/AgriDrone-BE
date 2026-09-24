using System.Text.Json;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class InviteSystemManagerCommandHandlerTests
{
    [Fact]
    public async Task HandleCreatesManagerAccountProfileAndPasswordSetupToken()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("  MANAGER@Example.COM "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.EmailSent);
        Assert.Equal("manager@example.com", result.Value.Email);

        var user = Assert.IsType<User>(fixture.Users.Added);
        Assert.Equal("manager@example.com", user.Email);
        Assert.Equal("manager@example.com", user.FullName);
        Assert.Null(user.Phone);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Single(user.UserRoles);

        var profile = Assert.IsType<SystemManagerProfile>(fixture.Profiles.Added);
        Assert.Equal(user.Id, profile.UserId);
        Assert.Equal(SystemManagerProfileStatus.Suspended, profile.Status);
        Assert.Equal(SystemManagerAvailabilityStatus.Unavailable, profile.Availability);
        Assert.Equal(FlightQualificationStatus.Pending, profile.QualificationStatus);

        var token = Assert.IsType<PasswordResetToken>(fixture.Tokens.Added);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(FakeTokenService.TokenHash, token.TokenHash);
        Assert.Equal(FakeTokenService.PlainTextToken, fixture.Email.PlainTextToken);
        Assert.Single(fixture.Audits.Items);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleRejectsAnEmailThatAlreadyExists()
    {
        var fixture = CreateFixture();
        fixture.Users.Existing = User.Create(
            "manager@example.com",
            "hash",
            "Existing User",
            null,
            UserStatus.Active,
            DateTimeOffset.UtcNow);

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("manager@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("User.EmailAlreadyExists", result.Error.Code);
        Assert.Null(fixture.Users.Added);
        Assert.Null(fixture.Profiles.Added);
        Assert.Null(fixture.Tokens.Added);
    }

    [Fact]
    public async Task HandleKeepsCreatedAccountWhenEmailDeliveryFails()
    {
        var fixture = CreateFixture();
        fixture.Email.Exception = new InvalidOperationException("SMTP unavailable");

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("manager@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.EmailSent);
        Assert.NotNull(fixture.Users.Added);
        Assert.NotNull(fixture.Profiles.Added);
        Assert.NotNull(fixture.Tokens.Added);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    private static Fixture CreateFixture()
    {
        var users = new FakeUserRepository();
        var profiles = new FakeProfileRepository();
        var tokens = new FakeTokenRepository();
        var email = new FakeEmailDelivery();
        var unitOfWork = new FakeUnitOfWork();
        var audits = new FakeAuditLogSink();

        var handler = new InviteSystemManagerCommandHandler(
            users,
            new FakeRoleRepository(CreateRole()),
            profiles,
            new FakePasswordService(),
            new FakeTokenService(),
            tokens,
            email,
            Options.Create(new PasswordResetOptions
            {
                ResetUrl = "https://example.com/reset-password",
                ExpirationMinutes = 30
            }),
            unitOfWork,
            new FakeAuditWriter(),
            audits,
            new FakeExecutionContext(),
            TimeProvider.System,
            NullLogger<InviteSystemManagerCommandHandler>.Instance);

        return new Fixture(handler, users, profiles, tokens, email, unitOfWork, audits);
    }

    private static Role CreateRole()
    {
        var role = Assert.IsType<Role>(
            Activator.CreateInstance(typeof(Role), nonPublic: true));
        typeof(Role).GetProperty(nameof(Role.Id))!.SetValue(role, Guid.NewGuid());
        return role;
    }

    private sealed record Fixture(
        InviteSystemManagerCommandHandler Handler,
        FakeUserRepository Users,
        FakeProfileRepository Profiles,
        FakeTokenRepository Tokens,
        FakeEmailDelivery Email,
        FakeUnitOfWork UnitOfWork,
        FakeAuditLogSink Audits);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? Existing { get; set; }
        public User? Added { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing);

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<User>>([]);

        public void Add(User user) => Added = user;

        public void Update(User user)
        {
        }
    }

    private sealed class FakeRoleRepository(Role role) : IRoleRepository
    {
        public Task<Role?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Role?>(role);

        public Task<bool> HasAssignedActiveUserAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeProfileRepository : ISystemManagerProfileRepository
    {
        public SystemManagerProfile? Added { get; private set; }

        public Task<SystemManagerProfile?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(null);

        public Task<SystemManagerProfile?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(null);

        public void Add(SystemManagerProfile profile) => Added = profile;
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public bool VerifyPassword(string password, string hashedPassword) => false;

        public string HashPassword(string password) => "hashed-random-password";
    }

    private sealed class FakeTokenService : IPasswordResetTokenService
    {
        public const string PlainTextToken = "plain-text-token";
        public const string TokenHash = "token-hash";

        public PasswordResetTokenResult Generate() => new(PlainTextToken, TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
    }

    private sealed class FakeTokenRepository : IPasswordResetTokenRepository
    {
        public PasswordResetToken? Added { get; private set; }

        public Task<PasswordResetToken?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task RevokeActiveForUserAsync(
            Guid userId,
            DateTimeOffset revokedAt,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryMarkUsedAsync(
            Guid tokenId,
            DateTimeOffset usedAt,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public void Add(PasswordResetToken passwordResetToken) => Added = passwordResetToken;
    }

    private sealed class FakeEmailDelivery : ISystemManagerInvitationEmailDelivery
    {
        public string? PlainTextToken { get; private set; }
        public Exception? Exception { get; set; }

        public Task DeliverAsync(
            string email,
            string plainTextToken,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            PlainTextToken = plainTextToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IIdentityUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class FakeAuditLogSink : IAuditLogSink
    {
        public List<AuditLog> Items { get; } = [];

        public void AddAuditLog(AuditLog auditLog) => Items.Add(auditLog);
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
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
            DateTimeOffset createdAt) =>
            throw new NotSupportedException();

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
            sink.AddAuditLog(
                AuditLog.ForSystemAdminAction(
                    actorId,
                    correlationId,
                    entityType,
                    entityId,
                    action,
                    oldData,
                    newData,
                    createdAt));
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId { get; } = Guid.NewGuid();
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
