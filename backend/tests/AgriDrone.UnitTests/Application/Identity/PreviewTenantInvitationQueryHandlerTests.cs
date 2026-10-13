using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class PreviewTenantInvitationQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PreviewRequiresAccountCreationWhenEmailIsNew()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("plain-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a***@example.com", result.Value.MaskedEmail);
        Assert.Equal(fixture.Tenant.Name, result.Value.TenantName);
        Assert.Equal(TenantMemberRole.Owner, result.Value.Role);
        Assert.Equal(fixture.Invitation.ExpiresAt, result.Value.ExpiresAt);
        Assert.True(result.Value.RequiresAccountCreation);
    }

    [Fact]
    public async Task PreviewUsesExistingAccountWithoutChangingCredentials()
    {
        var fixture = CreateFixture();
        fixture.UserRepository.User = User.Create(
            fixture.Invitation.Email,
            "existing-password-hash",
            "Existing User",
            null,
            UserStatus.Active,
            Now.AddDays(-1));

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("plain-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequiresAccountCreation);
    }

    [Fact]
    public async Task PreviewRejectsInactiveTenant()
    {
        var fixture = CreateFixture();
        fixture.Tenant.Deactivate(Now.AddMinutes(-1));

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("plain-token"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.Inactive", result.Error.Code);
    }

    [Fact]
    public async Task PreviewRejectsInactiveExistingAccount()
    {
        var fixture = CreateFixture();
        fixture.UserRepository.User = User.Create(
            fixture.Invitation.Email,
            "existing-password-hash",
            "Inactive User",
            null,
            UserStatus.Inactive,
            Now.AddDays(-1));

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("plain-token"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantInvitation.UserInactive", result.Error.Code);
    }

    [Fact]
    public async Task PreviewRejectsInvalidInvitationToken()
    {
        var fixture = CreateFixture();
        fixture.InvitationRepository.Invitation = null;

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("invalid-token"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "TenantInvitation.InvalidOrExpired",
            result.Error.Code);
    }

    [Fact]
    public async Task PreviewRejectsLegacyStaffInvitation()
    {
        var fixture = CreateFixture(
            TenantMemberRole.TenantAdmin,
            TenantInvitationPurpose.Membership);

        var result = await fixture.Handler.Handle(
            new PreviewTenantInvitationQuery("plain-token"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantInvitation.InvalidOrExpired", result.Error.Code);
    }

    private static Fixture CreateFixture(
        TenantMemberRole role = TenantMemberRole.Owner,
        TenantInvitationPurpose purpose =
            TenantInvitationPurpose.OwnerProvisioning)
    {
        var tenant = Tenant.Create(
            "TENANT",
            "Tenant Name",
            GeneralStatus.Active,
            Now.AddDays(-1));
        var invitation = TenantInvitation.Create(
            tenant.Id,
            "admin@example.com",
            role,
            purpose,
            StubInvitationTokenService.TokenHash,
            Guid.NewGuid(),
            Now.AddHours(1),
            Now.AddMinutes(-10));
        var invitationRepository =
            new StubTenantInvitationRepository(invitation);
        var tenantRepository = new StubTenantRepository(tenant);
        var userRepository = new StubUserRepository();
        var handler = new PreviewTenantInvitationQueryHandler(
            new StubInvitationTokenService(),
            invitationRepository,
            tenantRepository,
            userRepository,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            tenant,
            invitation,
            invitationRepository,
            userRepository);
    }

    private sealed record Fixture(
        PreviewTenantInvitationQueryHandler Handler,
        Tenant Tenant,
        TenantInvitation Invitation,
        StubTenantInvitationRepository InvitationRepository,
        StubUserRepository UserRepository);

    private sealed class StubInvitationTokenService
        : IInvitationTokenService
    {
        public const string TokenHash = "token-hash";

        public InvitationTokenResult Generate() =>
            new("plain-token", TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
    }

    private sealed class StubTenantInvitationRepository(
        TenantInvitation invitation) : ITenantInvitationRepository
    {
        public TenantInvitation? Invitation { get; set; } = invitation;

        public Task<TenantInvitation?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult(Invitation);

        public Task<TenantInvitation?> GetPendingAsync(
            Guid tenantId,
            string email,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> GetPendingOwnerProvisioningAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> GetByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public void Add(TenantInvitation invitationToAdd) =>
            throw new NotSupportedException();
    }

    private sealed class StubTenantRepository(Tenant tenant)
        : ITenantRepository
    {
        public Task<Tenant?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(
                id == tenant.Id && tenant.Status == GeneralStatus.Active
                    ? tenant
                    : null);

        public Task<Tenant?> GetByIdIgnoreStatusAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(id == tenant.Id ? tenant : null);

        public Task<Tenant?> GetByCodeAsync(
            string tenantCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<Tenant?>(null);

        public void Add(Tenant tenantToAdd) =>
            throw new NotSupportedException();
    }

    private sealed class StubUserRepository : IUserRepository
    {
        public User? User { get; set; }

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(User);

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(User);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<List<User>>([]);

        public void Add(User user) => throw new NotSupportedException();

        public void Update(User user) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
