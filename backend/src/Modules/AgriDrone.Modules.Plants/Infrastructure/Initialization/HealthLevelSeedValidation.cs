using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Initialization;

internal sealed record HealthLevelSeedSnapshot(
    string Code,
    int? Rank,
    bool IsHealthy,
    bool IsActive);

internal sealed record HealthLevelSeedValidationResult(
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public string ErrorMessage =>
        "Core health-level seed validation failed: " +
        string.Join(" ", Errors);
}

internal static class HealthLevelSeedRules
{
    private static readonly HealthLevelSeedSnapshot[] RequiredSeeds =
    [
        new("UNKNOWN", null, IsHealthy: false, IsActive: true),
        new("HEALTHY", 0, IsHealthy: true, IsActive: true),
        new("MILD", 1, IsHealthy: false, IsActive: true),
        new("MODERATE", 2, IsHealthy: false, IsActive: true),
        new("SEVERE", 3, IsHealthy: false, IsActive: true)
    ];

    public static IReadOnlyCollection<string> RequiredCodes { get; } =
        RequiredSeeds.Select(seed => seed.Code).ToArray();

    public static HealthLevelSeedValidationResult Validate(
        IReadOnlyCollection<HealthLevelSeedSnapshot> actualSeeds)
    {
        ArgumentNullException.ThrowIfNull(actualSeeds);

        var errors = new List<string>();
        foreach (var expected in RequiredSeeds)
        {
            var matches = actualSeeds
                .Where(actual => actual.Code == expected.Code)
                .ToArray();

            if (matches.Length == 0)
            {
                errors.Add(
                    $"Required health level '{expected.Code}' is missing.");
                continue;
            }

            if (matches.Length > 1)
            {
                errors.Add(
                    $"Required health level '{expected.Code}' is duplicated.");
                continue;
            }

            var actual = matches[0];
            if (!actual.IsActive)
            {
                errors.Add(
                    $"Required health level '{expected.Code}' must be active.");
            }

            if (actual.Rank != expected.Rank ||
                actual.IsHealthy != expected.IsHealthy)
            {
                errors.Add(
                    $"Required health level '{expected.Code}' has invalid " +
                    $"semantics; expected Rank={FormatRank(expected.Rank)} and " +
                    $"IsHealthy={expected.IsHealthy}, actual " +
                    $"Rank={FormatRank(actual.Rank)} and " +
                    $"IsHealthy={actual.IsHealthy}.");
            }
        }

        return new HealthLevelSeedValidationResult(errors);
    }

    private static string FormatRank(int? rank) =>
        rank?.ToString(System.Globalization.CultureInfo.InvariantCulture) ??
        "null";
}

internal sealed class HealthLevelSeedValidator(
    PlantsDbContext dbContext)
{
    public async Task<HealthLevelSeedValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        var requiredCodes = HealthLevelSeedRules.RequiredCodes;
        var actualSeeds = await dbContext.HealthLevels
            .AsNoTracking()
            .Where(level => requiredCodes.Contains(level.Code))
            .Select(level => new HealthLevelSeedSnapshot(
                level.Code,
                level.Rank,
                level.IsHealthy,
                level.IsActive))
            .ToArrayAsync(cancellationToken);

        return HealthLevelSeedRules.Validate(actualSeeds);
    }
}
