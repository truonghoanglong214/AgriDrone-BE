using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Initialization;

internal sealed record PlantHealthLabelSeedSnapshot(
    string Code,
    PlantHealthLabelKind? LabelKind,
    string? DatasetVersion,
    string? AnnotationRuleVersion,
    string? TeamLabelLimitation,
    bool IsActive);

internal sealed record PlantHealthMasterDataValidationResult(
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public string ErrorMessage =>
        "Plant-health master-data validation failed: " +
        string.Join(" ", Errors);
}

internal static class PlantHealthMasterDataRules
{
    private static readonly IReadOnlyDictionary<string, PlantHealthLabelKind>
        RequiredLabels = new Dictionary<string, PlantHealthLabelKind>(
            StringComparer.Ordinal)
        {
            ["BROWN_SPOT"] = PlantHealthLabelKind.SuspectedDisease,
            ["ANTHRACNOSE"] = PlantHealthLabelKind.SuspectedDisease,
            ["SUNBURN"] = PlantHealthLabelKind.ConfusingNonDisease,
            ["MECHANICAL_SCAR"] = PlantHealthLabelKind.ConfusingNonDisease
        };

    public static IReadOnlyCollection<string> RequiredCodes { get; } =
        RequiredLabels.Keys.ToArray();

    public static PlantHealthMasterDataValidationResult Validate(
        IReadOnlyCollection<PlantHealthLabelSeedSnapshot> actualLabels)
    {
        ArgumentNullException.ThrowIfNull(actualLabels);

        var errors = new List<string>();
        foreach (var expected in RequiredLabels)
        {
            var matches = actualLabels
                .Where(actual => actual.Code == expected.Key)
                .ToArray();

            if (matches.Length != 1)
            {
                errors.Add(matches.Length == 0
                    ? $"Required plant-health label '{expected.Key}' is missing."
                    : $"Required plant-health label '{expected.Key}' is duplicated.");
                continue;
            }

            var actual = matches[0];
            if (!actual.IsActive)
                errors.Add($"Required plant-health label '{expected.Key}' must be active.");

            if (actual.LabelKind != expected.Value)
                errors.Add($"Plant-health label '{expected.Key}' has an invalid kind.");

            if (actual.DatasetVersion != PlantHealthLabelPolicy.DatasetVersion ||
                actual.AnnotationRuleVersion != PlantHealthLabelPolicy.AnnotationRuleVersion ||
                actual.TeamLabelLimitation != PlantHealthLabelPolicy.TeamLabelLimitation)
            {
                errors.Add(
                    $"Plant-health label '{expected.Key}' has invalid provenance metadata.");
            }
        }

        var unexpectedTargetLabels = actualLabels
            .Where(actual => !RequiredLabels.ContainsKey(actual.Code))
            .Select(actual => actual.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (unexpectedTargetLabels.Length > 0)
        {
            errors.Add(
                "Unexpected pilot plant-health labels: " +
                string.Join(", ", unexpectedTargetLabels) + ".");
        }

        return new PlantHealthMasterDataValidationResult(errors);
    }
}

internal sealed class PlantHealthMasterDataValidator(
    PlantsDbContext dbContext)
{
    public async Task<PlantHealthMasterDataValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        var targetLabels = await dbContext.PlantConditions
            .AsNoTracking()
            .Where(condition =>
                condition.PlantHealthLabelKind != null &&
                condition.IsActive)
            .Select(condition => new PlantHealthLabelSeedSnapshot(
                condition.Code,
                condition.PlantHealthLabelKind,
                condition.DatasetVersion,
                condition.AnnotationRuleVersion,
                condition.TeamLabelLimitation,
                condition.IsActive))
            .ToArrayAsync(cancellationToken);

        return PlantHealthMasterDataRules.Validate(targetLabels);
    }
}
