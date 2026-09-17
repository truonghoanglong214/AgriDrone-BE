using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class FarmMembershipTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DeactivateMakesAssignmentInactiveAndIncrementsVersion()
    {
        var membership = CreateMembership();

        var changed = membership.Deactivate(Now);

        Assert.True(changed);
        Assert.Equal(GeneralStatus.Inactive, membership.Status);
        Assert.Equal(2, membership.Version);
    }

    [Fact]
    public void DeactivateIsIdempotent()
    {
        var membership = CreateMembership();
        membership.Deactivate(Now);

        var changed = membership.Deactivate(Now.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(GeneralStatus.Inactive, membership.Status);
        Assert.Equal(2, membership.Version);
    }

    [Fact]
    public void DeactivateRevokesActiveSelectedZoneAssignments()
    {
        var membership = FarmMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [Guid.NewGuid(), Guid.NewGuid()],
            Guid.NewGuid(),
            Now.AddDays(-1));

        membership.Deactivate(Now);

        Assert.DoesNotContain(
            membership.ZoneAssignments,
            assignment => assignment.RevokedAt is null);
        Assert.All(
            membership.ZoneAssignments,
            assignment => Assert.Equal(Now, assignment.RevokedAt));
    }

    private static FarmMembership CreateMembership() =>
        FarmMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            Now.AddDays(-1));
}
