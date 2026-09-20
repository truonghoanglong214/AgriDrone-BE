using AgriDrone.Modules.Plants.Infrastructure.Initialization;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Plants;

public sealed class HealthLevelSeedValidationTests
{
    [Fact]
    public void CompleteRequiredSeedSetIsValid()
    {
        var result = HealthLevelSeedRules.Validate(CreateValidSeeds());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingUnknownSeedIsRejectedWithClearError()
    {
        var seeds = CreateValidSeeds()
            .Where(seed => seed.Code != "UNKNOWN")
            .ToArray();

        var result = HealthLevelSeedRules.Validate(seeds);

        Assert.False(result.IsValid);
        Assert.Contains(
            "Required health level 'UNKNOWN' is missing.",
            result.Errors);
    }

    [Fact]
    public void InactiveRequiredSeedIsRejectedWithClearError()
    {
        var seeds = CreateValidSeeds()
            .Select(seed => seed.Code == "UNKNOWN"
                ? seed with { IsActive = false }
                : seed)
            .ToArray();

        var result = HealthLevelSeedRules.Validate(seeds);

        Assert.False(result.IsValid);
        Assert.Contains(
            "Required health level 'UNKNOWN' must be active.",
            result.Errors);
    }

    [Fact]
    public void IncorrectUnknownSemanticsAreRejectedWithClearError()
    {
        var seeds = CreateValidSeeds()
            .Select(seed => seed.Code == "UNKNOWN"
                ? seed with { Rank = 0, IsHealthy = true }
                : seed)
            .ToArray();

        var result = HealthLevelSeedRules.Validate(seeds);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "Required health level 'UNKNOWN' has invalid semantics",
                StringComparison.Ordinal));
    }

    private static HealthLevelSeedSnapshot[] CreateValidSeeds() =>
    [
        new("UNKNOWN", null, IsHealthy: false, IsActive: true),
        new("HEALTHY", 0, IsHealthy: true, IsActive: true),
        new("MILD", 1, IsHealthy: false, IsActive: true),
        new("MODERATE", 2, IsHealthy: false, IsActive: true),
        new("SEVERE", 3, IsHealthy: false, IsActive: true)
    ];
}
