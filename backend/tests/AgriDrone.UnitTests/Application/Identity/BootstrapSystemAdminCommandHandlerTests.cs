using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;
using AgriDrone.Modules.Identity.Application.Features.BootstrapSystemAdmin;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class BootstrapSystemAdminCommandHandlerTests
{
    [Fact]
    public async Task HandleCreatesInitialSystemAdminAndPasswordSetupToken()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new BootstrapSystemAdminCommand(
                "  ADMIN@Example.COM ",
                "  System Administrator  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.Equal("admin@example.com", result.Value.Email);
        var user = Assert.IsType<User>(fixture.UserRepository.AddedUser);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal("System Administrator", user.FullName);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(FakePasswordService.PasswordHash, user.PasswordHash);
        var assignment = Assert.Single(user.UserRoles);
        Assert.Equal(fixture.Role!.Id, assignment.RoleId);
        var resetToken = Assert.IsType<PasswordResetToken>(
            fixture.PasswordResetTokenRepository.AddedToken);
        Assert.Equal(user.Id, resetToken.UserId);
        Assert.Equal(FakePasswordResetTokenService.TokenHash, resetToken.TokenHash);
        Assert.Equal(1, fixture.BootstrapLock.AcquireCount);
        Assert.Equal(1, fixture.UnitOfWork.TransactionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
        Assert.Equal("admin@example.com", fixture.EmailDelivery.Email);
        Assert.Equal(
            FakePasswordResetTokenService.PlainTextToken,
            fixture.EmailDelivery.PlainTextToken);
    }

    [Fact]
    public async Task HandleIsNoOpWhenAnActiveSystemAdminExists()
    {
        var fixture = CreateFixture();
        fixture.RoleRepository.HasActiveSystemAdmin = true;

        var result = await fixture.Handler.Handle(
            new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Created);
        Assert.Null(fixture.UserRepository.AddedUser);
        Assert.Null(fixture.EmailDelivery.Email);
        Assert.Equal(0, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleFailsWhenSeededSystemRoleIsMissing()
    {
        var fixture = CreateFixture(roleExists: false);

        var result = await fixture.Handler.Handle(
            new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Bootstrap.SystemAdminRoleMissing", result.Error.Code);
        Assert.Null(fixture.UserRepository.AddedUser);
    }

    [Fact]
    public async Task HandleDoesNotPromoteAnExistingUser()
    {
        var fixture = CreateFixture();
        fixture.UserRepository.ExistingUser = User.Create(
            "admin@example.com",
            "existing-hash",
            "Existing User",
            null,
            UserStatus.Active,
            DateTimeOffset.UtcNow);

        var result = await fixture.Handler.Handle(
            new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Bootstrap.ConfiguredEmailAlreadyExists",
            result.Error.Code);
        Assert.Empty(fixture.UserRepository.ExistingUser.UserRoles);
        Assert.Null(fixture.UserRepository.AddedUser);
    }

    [Fact]
    public async Task HandleKeepsCreatedAdminWhenEmailDeliveryFails()
    {
        var fixture = CreateFixture();
        fixture.EmailDelivery.Exception = new InvalidOperationException(
            "SMTP unavailable");

        var result = await fixture.Handler.Handle(
            new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.NotNull(fixture.UserRepository.AddedUser);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    private static Fixture CreateFixture(bool roleExists = true)
    {
        var role = roleExists ? CreateSystemAdminRole() : null;
        var userRepository = new FakeUserRepository();
        var roleRepository = new FakeRoleRepository(role);
        var bootstrapLock = new FakeSystemAdminBootstrapLock();
        var passwordResetTokenRepository =
            new FakePasswordResetTokenRepository();
        var emailDelivery = new FakePasswordResetEmailDelivery();
        var unitOfWork = new FakeIdentityUnitOfWork();

        var handler = new BootstrapSystemAdminCommandHandler(
            userRepository,
            roleRepository,
            bootstrapLock,
            new FakePasswordService(),
            new FakePasswordResetTokenService(),
            passwordResetTokenRepository,
            emailDelivery,
            Options.Create(new PasswordResetOptions
            {
                ResetUrl = "https://example.com/reset-password",
                ExpirationMinutes = 30
            }),
            unitOfWork,
            NullLogger<BootstrapSystemAdminCommandHandler>.Instance);

        return new Fixture(
            handler,
            role,
            userRepository,
            roleRepository,
            bootstrapLock,
            passwordResetTokenRepository,
            emailDelivery,
            unitOfWork);
    }

    private static Role CreateSystemAdminRole()
    {
        var role = Assert.IsType<Role>(
            Activator.CreateInstance(typeof(Role), nonPublic: true));
        typeof(Role).GetProperty(nameof(Role.Id))!
            .SetValue(role, Guid.NewGuid());

        return role;
    }

    private sealed record Fixture(
        BootstrapSystemAdminCommandHandler Handler,
        Role? Role,
        FakeUserRepository UserRepository,
        FakeRoleRepository RoleRepository,
        FakeSystemAdminBootstrapLock BootstrapLock,
        FakePasswordResetTokenRepository PasswordResetTokenRepository,
        FakePasswordResetEmailDelivery EmailDelivery,
        FakeIdentityUnitOfWork UnitOfWork);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ExistingUser { get; set; }

        public User? AddedUser { get; private set; }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingUser);

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingUser);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<List<User>>([]);

        public void Add(User user) => AddedUser = user;

        public void Update(User user)
        {
        }
    }

    private sealed class FakeRoleRepository(Role? role) : IRoleRepository
    {
        public bool HasActiveSystemAdmin { get; set; }

        public Task<Role?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(role);

        public Task<bool> HasAssignedActiveUserAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HasActiveSystemAdmin);
    }

    private sealed class FakeSystemAdminBootstrapLock
        : ISystemAdminBootstrapLock
    {
        public int AcquireCount { get; private set; }

        public Task AcquireAsync(
            CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public const string PasswordHash = "bootstrap-password-hash";

        public bool VerifyPassword(string password, string hashedPassword) =>
            false;

        public string HashPassword(string password) => PasswordHash;
    }

    private sealed class FakePasswordResetTokenService
        : IPasswordResetTokenService
    {
        public const string PlainTextToken = "plain-text-token";
        public const string TokenHash = "token-hash";

        public PasswordResetTokenResult Generate() =>
            new(PlainTextToken, TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
    }

    private sealed class FakePasswordResetTokenRepository
        : IPasswordResetTokenRepository
    {
        public PasswordResetToken? AddedToken { get; private set; }

        public Task<PasswordResetToken?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task RevokeActiveForUserAsync(
            Guid userId,
            DateTimeOffset revokedAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> TryMarkUsedAsync(
            Guid tokenId,
            DateTimeOffset usedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Add(PasswordResetToken passwordResetToken) =>
            AddedToken = passwordResetToken;
    }

    private sealed class FakePasswordResetEmailDelivery
        : IPasswordResetEmailDelivery
    {
        public string? Email { get; private set; }

        public string? PlainTextToken { get; private set; }

        public Exception? Exception { get; set; }

        public Task DeliverAsync(
            string email,
            string fullName,
            string plainTextToken,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Email = email;
            PlainTextToken = plainTextToken;
            return Task.CompletedTask;
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
}
