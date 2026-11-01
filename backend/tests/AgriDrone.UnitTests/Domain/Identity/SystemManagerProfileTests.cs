using AgriDrone.Modules.Identity.Domain.SystemManagers;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class SystemManagerProfileTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewProfileIsSuspendedAndUnavailable()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);

        Assert.Equal(SystemManagerProfileStatus.Suspended, profile.Status);
        Assert.Equal(SystemManagerAvailabilityStatus.Unavailable, profile.Availability);
        Assert.False(profile.CanBeAssigned(Now));
    }

    [Fact]
    public void ManagerMustBeActiveAndAvailableToBeAssigned()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);
        profile.Activate(Now, profile.Version);

        Assert.False(profile.CanBeAssigned(Now));

        profile.UpdateAvailability(
            SystemManagerAvailabilityStatus.Available,
            Now,
            profile.Version);

        Assert.True(profile.CanBeAssigned(Now));
    }

    [Fact]
    public void SuspendingProfileAlsoMakesItUnavailable()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);
        profile.Activate(Now, profile.Version);
        profile.UpdateAvailability(
            SystemManagerAvailabilityStatus.Available,
            Now,
            profile.Version);

        profile.Suspend(Now.AddMinutes(1), profile.Version);

        Assert.Equal(SystemManagerProfileStatus.Suspended, profile.Status);
        Assert.Equal(SystemManagerAvailabilityStatus.Unavailable, profile.Availability);
        Assert.False(profile.CanOperate(Now.AddMinutes(1)));
    }

    [Fact]
    public void MutationRejectsStaleVersion()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() =>
            profile.Activate(Now, expectedVersion: 99));
    }
}
