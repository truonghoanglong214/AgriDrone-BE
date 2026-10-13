using AgriDrone.Modules.Identity.Domain.SystemManagers;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class SystemManagerProfileTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewProfileIsSuspendedUnavailableAndPendingQualification()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);

        Assert.Equal(SystemManagerProfileStatus.Suspended, profile.Status);
        Assert.Equal(SystemManagerAvailabilityStatus.Unavailable, profile.Availability);
        Assert.Equal(FlightQualificationStatus.Pending, profile.QualificationStatus);
        Assert.False(profile.CanBeAssigned(Now));
    }

    [Fact]
    public void ManagerMustBeActiveAvailableAndCurrentlyQualifiedToBeAssigned()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);
        profile.UpdateQualification(
            FlightQualificationStatus.Qualified,
            Now.AddDays(30),
            Now,
            profile.Version);
        profile.Activate(Now, profile.Version);

        Assert.False(profile.CanBeAssigned(Now));

        profile.UpdateAvailability(
            SystemManagerAvailabilityStatus.Available,
            Now,
            profile.Version);

        Assert.True(profile.CanBeAssigned(Now));
        Assert.False(profile.CanBeAssigned(Now.AddDays(31)));
    }

    [Fact]
    public void SuspendingProfileAlsoMakesItUnavailable()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);
        profile.UpdateQualification(
            FlightQualificationStatus.Qualified,
            Now.AddDays(30),
            Now,
            profile.Version);
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
    public void QualifiedStatusRequiresFutureExpiry()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);

        Assert.Throws<ArgumentException>(() => profile.UpdateQualification(
            FlightQualificationStatus.Qualified,
            Now,
            Now,
            profile.Version));
    }

    [Fact]
    public void MutationRejectsStaleVersion()
    {
        var profile = SystemManagerProfile.Create(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() =>
            profile.Activate(Now, expectedVersion: 99));
    }
}
