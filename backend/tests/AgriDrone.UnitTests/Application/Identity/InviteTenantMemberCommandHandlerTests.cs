using AgriDrone.Modules.Identity.Application.Features.InviteTenantMember;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class InviteTenantMemberCommandHandlerTests
{
    private static readonly DateTimeOffset ExpiresAt =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleCreatesMemberInvitationForTenantAdmin()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var invitationService = new FakeTenantInvitationService
        {
            Result = Result.Success(
                new TenantInvitationCreated(
                    invitationId,
                    "member@example.com",
                    ExpiresAt))
        };
        var accessService = new FakeEffectiveAccessService();
        var handler = new InviteTenantMemberCommandHandler(
            new FakeExecutionContext(tenantId, actorId),
            accessService,
            invitationService);

        var result = await handler.Handle(
            new InviteTenantMemberCommand(" Member@Example.com "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(invitationId, result.Value.InvitationId);
        Assert.Equal("member@example.com", result.Value.Email);
        Assert.Equal(ExpiresAt, result.Value.ExpiresAt);
        Assert.Equal(TenantAccessLevel.Admin, accessService.RequiredAccess);

        var request = Assert.IsType<CreateTenantInvitationRequest>(
            invitationService.Request);
        Assert.Equal(tenantId, request.TenantId);
        Assert.Equal(actorId, request.InvitedByUserId);
        Assert.Equal(" Member@Example.com ", request.Email);
        Assert.Equal(TenantMemberRole.Member, request.Role);
        Assert.Equal(TenantInvitationPurpose.Membership, request.Purpose);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutTenantAdminAccess()
    {
        var invitationService = new FakeTenantInvitationService();
        var accessService = new FakeEffectiveAccessService
        {
            Decision = AccessDecision.Deny(
                AccessDenialReason.TenantRoleInsufficient)
        };
        var handler = new InviteTenantMemberCommandHandler(
            new FakeExecutionContext(Guid.NewGuid(), Guid.NewGuid()),
            accessService,
            invitationService);

        var result = await handler.Handle(
            new InviteTenantMemberCommand("member@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.AccessDenied", result.Error.Code);
        Assert.Null(invitationService.Request);
    }

    [Fact]
    public async Task HandleRejectsMissingTenantContext()
    {
        var invitationService = new FakeTenantInvitationService();
        var accessService = new FakeEffectiveAccessService();
        var handler = new InviteTenantMemberCommandHandler(
            new FakeExecutionContext(null, Guid.NewGuid()),
            accessService,
            invitationService);

        var result = await handler.Handle(
            new InviteTenantMemberCommand("member@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.ContextRequired", result.Error.Code);
        Assert.Null(accessService.RequiredAccess);
        Assert.Null(invitationService.Request);
    }

    private sealed class FakeTenantInvitationService
        : ITenantInvitationService
    {
        public CreateTenantInvitationRequest? Request { get; private set; }

        public Result<TenantInvitationCreated> Result { get; set; } = null!;

        public Task<Result<TenantInvitationCreated>> InviteAsync(
            CreateTenantInvitationRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } = AccessDecision.Allow();

        public TenantAccessLevel? RequiredAccess { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            RequiredAccess = requiredAccess;
            return Task.FromResult(Decision);
        }

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
        Guid? tenantId,
        Guid? actorId) : IExecutionContext
    {
        public bool IsInitialized => true;

        public Guid? TenantId => tenantId;

        public Guid? ActorId => actorId;

        public Guid CorrelationId { get; } = Guid.NewGuid();

        public Guid? MessageId => null;

        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }
}
