using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Farms;

public sealed class FarmOptimisticConcurrencyTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateStartsAtVersionOne()
    {
        var farm = CreateFarm(GeneralStatus.Active);

        Assert.Equal(1, farm.Version);
    }

    [Fact]
    public void UpdateDetailsIncrementsVersion()
    {
        var farm = CreateFarm(GeneralStatus.Active);

        farm.UpdateDetails(
            "Updated farm",
            "Updated address",
            boundary: null,
            centerPoint: null,
            areaHectares: 2.5m,
            CreatedAt.AddMinutes(1));

        Assert.Equal(2, farm.Version);
    }

    [Fact]
    public void ActivateIncrementsVersionOnlyWhenStateChanges()
    {
        var farm = CreateFarm(GeneralStatus.Inactive);

        Assert.True(farm.Activate(CreatedAt.AddMinutes(1)));
        Assert.Equal(2, farm.Version);

        Assert.False(farm.Activate(CreatedAt.AddMinutes(2)));
        Assert.Equal(2, farm.Version);
    }

    [Fact]
    public void ArchiveIncrementsVersionOnlyWhenStateChanges()
    {
        var farm = CreateFarm(GeneralStatus.Active);

        Assert.True(farm.Archive(CreatedAt.AddMinutes(1)));
        Assert.Equal(2, farm.Version);

        Assert.False(farm.Archive(CreatedAt.AddMinutes(2)));
        Assert.Equal(2, farm.Version);
    }

    private static Farm CreateFarm(GeneralStatus status) =>
        Farm.Create(
            Guid.NewGuid(),
            "FARM-001",
            "Test farm",
            "Test address",
            boundary: null,
            centerPoint: null,
            areaHectares: 1.5m,
            status,
            Guid.NewGuid(),
            CreatedAt);
}
