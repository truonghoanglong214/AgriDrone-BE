using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Farms;

public sealed class FarmZoneOptimisticConcurrencyTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateStartsAtVersionOne()
    {
        var zone = CreateZone();

        Assert.Equal(1, zone.Version);
    }

    [Fact]
    public void UpdateDetailsIncrementsVersion()
    {
        var zone = CreateZone();

        zone.UpdateDetails(
            "ZONE-002",
            "Updated zone",
            boundary: null,
            areaHectares: 2.5m,
            CreatedAt.AddMinutes(1));

        Assert.Equal(2, zone.Version);
    }

    private static FarmZone CreateZone() =>
        FarmZone.Create(
            "ZONE-001",
            "Test zone",
            boundary: null,
            areaHectares: 1.5m,
            GeneralStatus.Active,
            Guid.NewGuid(),
            CreatedAt);
}
