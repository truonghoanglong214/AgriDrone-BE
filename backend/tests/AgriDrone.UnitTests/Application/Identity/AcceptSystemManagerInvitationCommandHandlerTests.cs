using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using AgriDrone.Modules.Identity.Application.Features.AcceptSystemManagerInvitation;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class AcceptSystemManagerInvitationCommandHandlerTests
{
    [Fact]
    public async Task HandleCreatesAccountRoleAndProfileAndConsumesInvitationOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = SystemManagerInvitation.Create(
            "manager@example.com",
            FakeInvitationTokenService.TokenHash,
            Guid.NewGuid(),
            now.AddHours(24),
            now);
        var users = new FakeUserRepository();
        var profiles = new FakeProfileRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new AcceptSystemManagerInvitationCommandHandler(
            new FakeInvitationTokenService(),
            new FakeInvitationRepository(invitation),
            users,
            new FakeRoleRepository(CreateRole()),
            profiles,
            new FakePasswordService(),
            TimeProvider.System,
            unitOfWork);
        var command = new AcceptSystemManagerInvitationCommand(
            FakeInvitationTokenService.PlainTextToken,
            "Password123!",
            "Manager Name",
            "0901234567");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AccountCreated);
        Assert.Equal(SystemManagerInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(users.Added!.Id, invitation.AcceptedByUserId);
        Assert.Equal("hashed-password", users.Added.PasswordHash);
        Assert.Single(users.Added.UserRoles);
        Assert.Equal(users.Added.Id, profiles.Added!.UserId);
        Assert.Equal(profiles.Added.Id, result.Value.ProfileId);
        Assert.Equal(1, unitOfWork.SaveChangesCount);

        var reusedResult = await handler.Handle(command, CancellationToken.None);

        Assert.True(reusedResult.IsFailure);
        Assert.Equal(
            "SystemManagerInvitation.InvalidOrExpired",
            reusedResult.Error.Code);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    private static Role CreateRole()
    {
        var role = Assert.IsType<Role>(
            Activator.CreateInstance(typeof(Role), nonPublic: true));
        typeof(Role).GetProperty(nameof(Role.Id))!
            .SetValue(role, Guid.NewGuid());
        return role;
    }

    private sealed class FakeInvitationTokenService : IInvitationTokenService
    {
        public const string PlainTextToken = "plain-text-token";
        public const string TokenHash =
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

        public InvitationTokenResult Generate() =>
            new(PlainTextToken, TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
    }

    private sealed class FakeInvitationRepository(
        SystemManagerInvitation invitation)
        : ISystemManagerInvitationRepository
    {
        public Task<SystemManagerInvitation?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerInvitation?>(invitation);

        public Task<SystemManagerInvitation?> GetPendingByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerInvitation?>(invitation);

        public void Add(SystemManagerInvitation value)
        {
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? Added { get; private set; }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Added?.Id == id ? Added : null);

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Added is not null && Added.Email == email ? Added : null);

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            GetByEmailAsync(email, cancellationToken);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Added is null ? [] : new List<User> { Added });

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
            Task.FromResult(Added?.Id == id ? Added : null);

        public Task<SystemManagerProfile?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(null);

        public void Add(SystemManagerProfile profile) => Added = profile;
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public bool VerifyPassword(string password, string hashedPassword) =>
            false;

        public string HashPassword(string password) => "hashed-password";
    }

    private sealed class FakeUnitOfWork : IIdentityUnitOfWork
    {
        public int SaveChangesCount { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
