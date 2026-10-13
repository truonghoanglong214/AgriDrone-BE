using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class SystemManagerAccessServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AssignedManagerCanResolveFarmWithoutTenantContext()
    {
        var fixture = CreateFixture();

        var decision = await fixture.Service.ResolveFarmAccessAsync(fixture.FarmId);

        Assert.True(decision.IsAllowed);
        Assert.Equal(fixture.TenantId, decision.TenantId);
        Assert.Null(fixture.ExecutionContext.TenantId);
    }

    [Fact]
    public async Task ManagerCannotReadFarmAssignedToAnotherManager()
    {
        var fixture = CreateFixture(actorIsAssignedManager: false);

        var decision = await fixture.Service.ResolveFarmAccessAsync(fixture.FarmId);

        Assert.False(decision.IsAllowed);
        Assert.Null(decision.TenantId);
    }

    private static Fixture CreateFixture(bool actorIsAssignedManager = true)
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var actor = User.Create(
            "manager-a@example.com",
            "hash",
            "Manager A",
            null,
            UserStatus.Active,
            Now);
        var actorProfile = CreateOperationalProfile(actor.Id);
        var assignedProfile = actorIsAssignedManager
            ? actorProfile
            : CreateOperationalProfile(Guid.NewGuid());
        var assignment = FarmManagerAssignment.Create(
            tenantId,
            farmId,
            assignedProfile.Id,
            Guid.NewGuid(),
            "Primary assignment",
            Now);

        var executionContext = new FakeExecutionContext(actor.Id);
        var service = new SystemManagerAccessService(
            executionContext,
            new FakeFarmQuery(new SystemManagerFarmReference(
                tenantId, farmId, "F-01", "Farm 01", null, 2.5m)),
            new FakeProfileRepository(actorProfile),
            new FakeAssignmentRepository(assignment),
            new FakeUserRepository(actor),
            new FixedTimeProvider(Now));

        return new Fixture(service, executionContext, tenantId, farmId);
    }

    private static SystemManagerProfile CreateOperationalProfile(Guid userId)
    {
        var profile = SystemManagerProfile.Create(userId, Now);
        profile.UpdateQualification(
            FlightQualificationStatus.Qualified,
            Now.AddYears(1),
            Now,
            profile.Version);
        profile.Activate(Now, profile.Version);
        return profile;
    }

    private sealed record Fixture(
        SystemManagerAccessService Service,
        FakeExecutionContext ExecutionContext,
        Guid TenantId,
        Guid FarmId);

    private sealed class FakeExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeFarmQuery(SystemManagerFarmReference farm)
        : IFarmAssignmentReferenceQuery
    {
        public Task<SystemManagerFarmReference?> GetActiveFarmAsync(
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerFarmReference?>(
                farm.FarmId == farmId ? farm : null);

        public Task<IReadOnlyCollection<SystemManagerFarmReference>> GetActiveFarmsAsync(
            IReadOnlyCollection<Guid> farmIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<SystemManagerFarmReference>>(
                farmIds.Contains(farm.FarmId) ? [farm] : []);

        public Task<bool> IsActiveFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(farm.TenantId == tenantId && farm.FarmId == farmId);

        public Task<IReadOnlyCollection<FarmAssignmentReference>> GetActiveFarmsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<FarmAssignmentReference>>([]);

        public Task<IReadOnlyCollection<FarmAssignmentZoneReference>> GetActiveZonesAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> farmIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<FarmAssignmentZoneReference>>([]);
    }

    private sealed class FakeProfileRepository(
        SystemManagerProfile assignedProfile) : ISystemManagerProfileRepository
    {
        public Task<SystemManagerProfile?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(
                assignedProfile.Id == id ? assignedProfile : null);

        public Task<SystemManagerProfile?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemManagerProfile?>(
                userId == assignedProfile.UserId ? assignedProfile : null);

        public void Add(SystemManagerProfile profile)
        {
        }
    }

    private sealed class FakeAssignmentRepository(FarmManagerAssignment assignment)
        : IFarmManagerAssignmentRepository
    {
        public Task<FarmManagerAssignment?> GetActiveByFarmIdAsync(
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FarmManagerAssignment?>(
                assignment.FarmId == farmId && assignment.IsActive
                    ? assignment
                    : null);

        public Task<IReadOnlyCollection<FarmManagerAssignment>> GetActiveByProfileIdAsync(
            Guid profileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<FarmManagerAssignment>>(
                assignment.SystemManagerProfileId == profileId ? [assignment] : []);

        public void Add(FarmManagerAssignment value)
        {
        }
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByEmailIncludingDeletedAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<IReadOnlyCollection<string>> GetSystemRoleCodesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<User> { user });

        public void Add(User value)
        {
        }

        public void Update(User value)
        {
        }
    }
}
