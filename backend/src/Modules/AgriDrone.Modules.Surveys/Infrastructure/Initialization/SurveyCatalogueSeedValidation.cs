using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Initialization;

internal sealed record SurveyServiceSeedSnapshot(
    string Code,
    SurveyServiceType ServiceType);

internal sealed record SurveyCatalogueSeedValidationResult(
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public string ErrorMessage =>
        "Survey catalogue seed validation failed: " +
        string.Join(" ", Errors);
}

internal static class SurveyCatalogueSeedRules
{
    private static readonly SurveyServiceSeedSnapshot[] RequiredSeeds =
    [
        new("PLANT_HEALTH", SurveyServiceType.PlantHealth),
        new("HARVEST_READINESS", SurveyServiceType.HarvestReadiness)
    ];

    public static IReadOnlyCollection<string> RequiredCodes { get; } =
        RequiredSeeds.Select(seed => seed.Code).ToArray();

    public static SurveyCatalogueSeedValidationResult Validate(
        IReadOnlyCollection<SurveyServiceSeedSnapshot> actualSeeds)
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
                    $"Required survey service '{expected.Code}' is missing.");
                continue;
            }

            if (matches.Length > 1)
            {
                errors.Add(
                    $"Required survey service '{expected.Code}' is duplicated.");
                continue;
            }

            if (matches[0].ServiceType != expected.ServiceType)
            {
                errors.Add(
                    $"Required survey service '{expected.Code}' has service " +
                    $"type '{matches[0].ServiceType}' instead of " +
                    $"'{expected.ServiceType}'.");
            }
        }

        return new SurveyCatalogueSeedValidationResult(errors);
    }
}

internal sealed class SurveyCatalogueSeedValidator(
    SurveysDbContext dbContext)
{
    public async Task<SurveyCatalogueSeedValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        var requiredCodes = SurveyCatalogueSeedRules.RequiredCodes;
        var actualSeeds = await dbContext.SurveyServices
            .AsNoTracking()
            .Where(service => requiredCodes.Contains(service.Code))
            .Select(service => new SurveyServiceSeedSnapshot(
                service.Code,
                service.ServiceType))
            .ToArrayAsync(cancellationToken);

        return SurveyCatalogueSeedRules.Validate(actualSeeds);
    }
}
