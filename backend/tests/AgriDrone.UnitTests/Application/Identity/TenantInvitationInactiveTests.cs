using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Invitations;
using AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;
using AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using AgriDrone.SharedKernel.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class TenantInvitationInactiveTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AcceptInvitationRejectsInactiveTenant()
    {
        var tenant = CreateTenant(GeneralStatus.Inactive);
        var invitation = CreateInvitation(tenant.Id);
        var membershipRepository = new StubTenantMembershipRepository();
        var handler = new AcceptTenantInvitationCommandHandler(
            new StubInvitationTokenService(),
            new StubTenantInvitationRepository(invitation),
            new StubTenantRepository(tenant),
            new StubUserRepository(),
            membershipRepository,
            new StubPasswordService(),
            new StubIdentityIntegrationOutbox(),
            new StubExecutionContext(),
            new FixedTimeProvider(Now),
            new StubIdentityUnitOfWork());

        var result = await handler.Handle(
            new AcceptTenantInvitationCommand(
                StubInvitationTokenService.PlainTextToken,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.Inactive", result.Error.Code);
        Assert.False(membershipRepository.AddCalled);
    }

    [Fact]
    public async Task DeliverInvitationEmailSkipsInactiveTenant()
    {
        var tenant = CreateTenant(GeneralStatus.Inactive);
        var invitation = CreateInvitation(tenant.Id);
        var emailSender = new StubEmailSender();
        var delivery = new TenantInvitationEmailDelivery(
            new StubTenantInvitationRepository(invitation),
            new StubTenantRepository(tenant),
            new StubInvitationTokenService(),
            emailSender,
            Options.Create(new TenantInvitationOptions
            {
                AcceptUrl = "https://example.test/invitations/accept",
                ExpirationHours = 24
            }),
            new FixedTimeProvider(Now));

        var result = await delivery.DeliverAsync(
            tenant.Id,
            invitation.Id,
            StubInvitationTokenService.PlainTextToken,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Sent);
        Assert.Equal(
            "The invitation tenant is inactive.",
            result.Value.SkipReason);
        Assert.Empty(emailSender.SentMessages);
    }

    [Fact]
    public async Task AcceptInvitationRejectsLegacyStaffInvitation()
    {
        var tenant = CreateTenant(GeneralStatus.Active);
        var invitation = TenantInvitation.Create(
            tenant.Id,
            "admin@example.com",
            TenantMemberRole.TenantAdmin,
            TenantInvitationPurpose.Membership,
            StubInvitationTokenService.TokenHash,
            Guid.NewGuid(),
            Now.AddHours(1),
            Now.AddMinutes(-10));
        var membershipRepository = new StubTenantMembershipRepository();
        var handler = new AcceptTenantInvitationCommandHandler(
            new StubInvitationTokenService(),
            new StubTenantInvitationRepository(invitation),
            new StubTenantRepository(tenant),
            new StubUserRepository(),
            membershipRepository,
            new StubPasswordService(),
            new StubIdentityIntegrationOutbox(),
            new StubExecutionContext(),
            new FixedTimeProvider(Now),
            new StubIdentityUnitOfWork());

        var result = await handler.Handle(
            new AcceptTenantInvitationCommand(
                StubInvitationTokenService.PlainTextToken,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TenantInvitation.InvalidOrExpired", result.Error.Code);
        Assert.False(membershipRepository.AddCalled);
    }

    [Fact]
    public async Task DeliverInvitationEmailSkipsLegacyStaffInvitation()
    {
        var tenant = CreateTenant(GeneralStatus.Active);
        var invitation = TenantInvitation.Create(
            tenant.Id,
            "admin@example.com",
            TenantMemberRole.TenantAdmin,
            TenantInvitationPurpose.Membership,
            StubInvitationTokenService.TokenHash,
            Guid.NewGuid(),
            Now.AddHours(1),
            Now.AddMinutes(-10));
        var emailSender = new StubEmailSender();
        var delivery = new TenantInvitationEmailDelivery(
            new StubTenantInvitationRepository(invitation),
            new StubTenantRepository(tenant),
            new StubInvitationTokenService(),
            emailSender,
            Options.Create(new TenantInvitationOptions
            {
                AcceptUrl = "https://example.test/invitations/accept",
                ExpirationHours = 24
            }),
            new FixedTimeProvider(Now));

        var result = await delivery.DeliverAsync(
            tenant.Id,
            invitation.Id,
            StubInvitationTokenService.PlainTextToken,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Sent);
        Assert.Equal(
            "Legacy staff invitations are retired.",
            result.Value.SkipReason);
        Assert.Empty(emailSender.SentMessages);
    }

    private static Tenant CreateTenant(GeneralStatus status)
    {
        var tenant = Tenant.Create(
            "TENANT",
            "Tenant",
            GeneralStatus.Active,
            Now.AddHours(-1));

        if (status == GeneralStatus.Inactive)
        {
            tenant.Deactivate(Now.AddMinutes(-30));
        }

        return tenant;
    }

    private static TenantInvitation CreateInvitation(Guid tenantId) =>
        TenantInvitation.Create(
            tenantId,
            "admin@example.com",
            TenantMemberRole.Owner,
            TenantInvitationPurpose.OwnerProvisioning,
            StubInvitationTokenService.TokenHash,
            Guid.NewGuid(),
            Now.AddHours(1),
            Now.AddMinutes(-10));

    private sealed class StubInvitationTokenService
        : IInvitationTokenService
    {
        public const string PlainTextToken = "plain-text-token";
        public const string TokenHash = "token-hash";

        public InvitationTokenResult Generate() =>
            new(PlainTextToken, TokenHash);

        public string Hash(string plainTextToken) => TokenHash;
    }

    private sealed class StubTenantInvitationRepository(
        TenantInvitation invitation) : ITenantInvitationRepository
    {
        public Task<TenantInvitation?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(invitation);

        public Task<TenantInvitation?> GetByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(
                invitationId == invitation.Id ? invitation : null);

        public Task<TenantInvitation?> GetPendingAsync(
            Guid tenantId,
            string email,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantInvitation?>(null);

        public Task<TenantInvitation?> GetPendingOwnerProvisioningAsync(
            Guid tenantId,
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

    private sealed class StubTenantMembershipRepository
        : ITenantMembershipRepository
    {
        public bool AddCalled { get; private set; }

        public void Add(TenantMembership tenantMembership) =>
            AddCalled = true;

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

    private sealed class StubUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "User lookup must not occur for an inactive tenant.");

        public Task<User?> GetByEmailIncludingDeletedAsync(
            string email,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

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

    private sealed class StubPasswordService : IPasswordService
    {
        public bool VerifyPassword(string password, string hashedPassword) =>
            throw new NotSupportedException();

        public string HashPassword(string password) =>
            throw new NotSupportedException();
    }

    private sealed class StubIdentityIntegrationOutbox
        : IIdentityIntegrationOutbox
    {
        public void Add<TPayload>(
            IntegrationEventEnvelope<TPayload> envelope,
            string? partitionKey = null) =>
            throw new NotSupportedException();
    }

    private sealed class StubIdentityUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "No changes must be saved for an inactive tenant.");

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class StubExecutionContext : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => null;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class StubEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = [];

        public Task SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
