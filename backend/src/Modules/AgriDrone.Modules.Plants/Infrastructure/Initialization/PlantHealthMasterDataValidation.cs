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

        // 1. Verify all required baseline labels exist and are active
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

            if (string.IsNullOrWhiteSpace(actual.DatasetVersion) ||
                string.IsNullOrWhiteSpace(actual.AnnotationRuleVersion) ||
                string.IsNullOrWhiteSpace(actual.TeamLabelLimitation))
            {
                errors.Add(
                    $"Plant-health label '{expected.Key}' has invalid provenance metadata.");
            }
        }

        // 2. Verify additional active labels are well-formed and unique
        var additionalLabels = actualLabels
            .Where(actual => !RequiredLabels.ContainsKey(actual.Code))
            .ToArray();

        var duplicates = actualLabels
            .GroupBy(label => label.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1 && !RequiredLabels.ContainsKey(group.Key))
            .Select(group => group.Key)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        foreach (var dup in duplicates)
        {
            errors.Add($"Plant-health label '{dup}' is duplicated.");
        }

        foreach (var label in additionalLabels)
        {
            if (!label.IsActive)
                errors.Add($"Plant-health label '{label.Code}' must be active.");

            if (!label.LabelKind.HasValue || !Enum.IsDefined(label.LabelKind.Value))
                errors.Add($"Plant-health label '{label.Code}' has an invalid kind.");

            if (string.IsNullOrWhiteSpace(label.DatasetVersion) ||
                string.IsNullOrWhiteSpace(label.AnnotationRuleVersion) ||
                string.IsNullOrWhiteSpace(label.TeamLabelLimitation))
            {
                errors.Add(
                    $"Plant-health label '{label.Code}' has invalid provenance metadata.");
            }
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

        var result = PlantHealthMasterDataRules.Validate(targetLabels);
        var hasUnknownHealthLevel = await dbContext.HealthLevels
            .AsNoTracking()
            .AnyAsync(level => level.Code == "UNKNOWN", cancellationToken);

        return hasUnknownHealthLevel
            ? result
            : new PlantHealthMasterDataValidationResult(
                [..result.Errors, "Required health level 'UNKNOWN' is missing."]);
    }
}
