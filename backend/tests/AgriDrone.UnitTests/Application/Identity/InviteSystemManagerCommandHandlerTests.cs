using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class InviteSystemManagerCommandHandlerTests
{
    [Fact]
    public async Task HandleCreatesInvitationWithoutCreatingAccountOrProfile()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("  MANAGER@Example.COM "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.EmailSent);
        Assert.Equal("manager@example.com", result.Value.Email);
        Assert.Equal(fixture.Invitations.Added!.Id, result.Value.InvitationId);
        Assert.Equal("manager@example.com", fixture.Invitations.Added.Email);
        Assert.Equal(FakeInvitationTokenService.TokenHash, fixture.Invitations.Added.TokenHash);
        Assert.Equal(FakeInvitationTokenService.PlainTextToken, fixture.Email.PlainTextToken);
        Assert.Null(fixture.Users.Added);
        Assert.Null(fixture.Profiles.Added);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAllowsExistingActiveUserWithoutManagerProfile()
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

        Assert.True(result.IsSuccess);
        Assert.NotNull(fixture.Invitations.Added);
        Assert.Null(fixture.Users.Added);
        Assert.Null(fixture.Profiles.Added);
    }

    [Fact]
    public async Task HandleRejectsUserWithExistingManagerProfile()
    {
        var fixture = CreateFixture();
        var user = User.Create(
            "manager@example.com",
            "hash",
            "Existing Manager",
            null,
            UserStatus.Active,
            DateTimeOffset.UtcNow);
        fixture.Users.Existing = user;
        fixture.Profiles.Existing = SystemManagerProfile.Create(
            user.Id,
            DateTimeOffset.UtcNow);

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("manager@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "SystemManagerInvitation.AlreadySystemManager",
            result.Error.Code);
        Assert.Null(fixture.Invitations.Added);
    }

    [Fact]
    public async Task HandleKeepsPendingInvitationWhenEmailDeliveryFails()
    {
        var fixture = CreateFixture();
        fixture.Email.Exception = new InvalidOperationException("SMTP unavailable");

        var result = await fixture.Handler.Handle(
            new InviteSystemManagerCommand("manager@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.EmailSent);
        Assert.NotNull(fixture.Invitations.Added);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCount);
    }

    private static Fixture CreateFixture()
    {
        var users = new FakeUserRepository();
        var profiles = new FakeProfileRepository();
        var invitations = new FakeInvitationRepository();
        var email = new FakeEmailDelivery();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new InviteSystemManagerCommandHandler(
            users,
            profiles,
            invitations,
            new FakeInvitationTokenService(),
            email,
            Options.Create(new SystemManagerInvitationOptions
            {
                AcceptUrl = "https://example.test/accept-system-manager-invitation",
                ExpirationHours = 24
            }),
            unitOfWork,
            new FakeExecutionContext(),
            TimeProvider.System,
            NullLogger<InviteSystemManagerCommandHandler>.Instance);

        return new Fixture(handler, users, profiles, invitations, email, unitOfWork);
    }

    private sealed record Fixture(
        InviteSystemManagerCommandHandler Handler,
        FakeUserRepository Users,
        FakeProfileRepository Profiles,
        FakeInvitationRepository Invitations,
        FakeEmailDelivery Email,
        FakeUnitOfWork UnitOfWork);

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

    private sealed class FakeProfileRepository : ISystemManagerProfileRepository
    {
        public SystemManagerProfile? Existing { get; set; }
        public SystemManagerProfile? Added { get; private set; }

        public Task<SystemManagerProfile?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(null);

        public Task<SystemManagerProfile?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing);

        public void Add(SystemManagerProfile profile) => Added = profile;
    }

    private sealed class FakeInvitationRepository : ISystemManagerInvitationRepository
    {
        public SystemManagerInvitation? Pending { get; set; }
        public SystemManagerInvitation? Added { get; private set; }

        public Task<SystemManagerInvitation?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerInvitation?>(null);

        public Task<SystemManagerInvitation?> GetPendingByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Pending);

        public void Add(SystemManagerInvitation invitation) => Added = invitation;
    }

    private sealed class FakeInvitationTokenService : IInvitationTokenService
    {
        public const string PlainTextToken = "plain-text-token";
        public const string TokenHash =
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

        public InvitationTokenResult Generate() => new(PlainTextToken, TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
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
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
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
