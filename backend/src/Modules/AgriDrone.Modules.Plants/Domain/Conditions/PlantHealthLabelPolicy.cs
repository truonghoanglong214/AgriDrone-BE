namespace AgriDrone.Modules.Plants.Domain.Conditions;

public static class PlantHealthLabelPolicy
{
    public const string DatasetVersion = "AGRIDRONE_PILOT_DATASET_V1";
    public const string AnnotationRuleVersion = "AGRIDRONE_ANNOTATION_RULES_V1";
    public const string TeamLabelLimitation =
        "Labels are produced by the AgriDrone team for pilot evaluation and are not expert-confirmed diagnoses.";

    public static IReadOnlySet<string> SuspectedDiseaseCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "BROWN_SPOT",
            "ANTHRACNOSE"
        };

    public static IReadOnlySet<string> ConfusingNonDiseaseCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SUNBURN",
            "MECHANICAL_SCAR"
        };

    public static bool IsAllowed(string code, PlantHealthLabelKind kind)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        return kind switch
        {
            PlantHealthLabelKind.SuspectedDisease =>
                SuspectedDiseaseCodes.Contains(normalizedCode),
            PlantHealthLabelKind.ConfusingNonDisease =>
                ConfusingNonDiseaseCodes.Contains(normalizedCode),
            _ => false
        };
    }
}
