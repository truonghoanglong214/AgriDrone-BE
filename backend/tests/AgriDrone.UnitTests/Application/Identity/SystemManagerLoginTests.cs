using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class SystemManagerLoginTests
{
    [Fact]
    public async Task SystemManagerGetsSystemSessionWithoutTenantMembership()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            "manager@example.com",
            "stored-hash",
            "System Manager",
            null,
            UserStatus.Active,
            now);
        var users = new FakeUserRepository(user);
        var memberships = new FakeTenantMembershipRepository();
        var tokens = new CapturingJwtTokenGenerator(now.AddHours(1));
        var handler = new LoginUserCommandHandler(
            users,
            memberships,
            new AcceptingPasswordService(),
            tokens,
            new UnusedTenantSelectionTokenService());

        var result = await handler.Handle(
            new LoginUserCommand(user.Email, "password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Session);
        Assert.Null(result.Value.TenantSelection);
        Assert.False(memberships.WasCalled);
        Assert.NotNull(tokens.Request);
        Assert.Null(tokens.Request.TenantId);
        Assert.Null(tokens.Request.TenantMembershipId);
        Assert.Contains(SystemRoles.SystemManager, tokens.Request.SystemRoles);
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Email == email ? user : null);

        public Task<User?> GetByEmailIncludingDeletedAsync(string email, CancellationToken cancellationToken = default) =>
            GetByEmailAsync(email, cancellationToken);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([SystemRoles.SystemManager]);

        public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<User> { user });

        public void Add(User value)
        {
        }

        public void Update(User value)
        {
        }
    }

    private sealed class FakeTenantMembershipRepository : ITenantMembershipRepository
    {
        public bool WasCalled { get; private set; }

        public void Add(TenantMembership tenantMembership)
        {
        }

        public Task<IReadOnlyCollection<TenantMembership>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<IReadOnlyCollection<TenantMembership>>([]);
        }

        public Task<TenantMembership?> GetActiveByUserAndTenantIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<TenantMembership?>(null);
        public Task<bool> HasActiveOwnerAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<TenantMembership?> GetByUserAndTenantIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<TenantMembership?>(null);
        public Task<TenantMembership?> GetByIdAsync(Guid membershipId, CancellationToken cancellationToken) => Task.FromResult<TenantMembership?>(null);
        public Task<TenantMembership?> GetActiveOwnerAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<TenantMembership?>(null);
    }

    private sealed class AcceptingPasswordService : IPasswordService
    {
        public bool VerifyPassword(string password, string hashedPassword) => true;
        public string HashPassword(string password) => "hash";
    }

    private sealed class CapturingJwtTokenGenerator(DateTimeOffset expiresAt)
        : IJwtTokenGenerator
    {
        public AccessTokenRequest? Request { get; private set; }

        public AccessTokenResult GenerateAccessToken(AccessTokenRequest request)
        {
            Request = request;
            return new AccessTokenResult("access-token", expiresAt);
        }
    }

    private sealed class UnusedTenantSelectionTokenService
        : ITenantSelectionTokenService
    {
        public TenantSelectionTokenResult Generate(Guid userId) =>
            throw new InvalidOperationException("Tenant selection must not be used for a SystemManager.");

        public Guid? Validate(string token) => null;
    }
}
