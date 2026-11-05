namespace AgriDrone.Modules.Plants.Domain.Conditions;

public static class PlantHealthLabelPolicy
{
    public const string DefaultDatasetVersion = "AGRIDRONE_PILOT_DATASET_V1";
    public const string DefaultAnnotationRuleVersion = "AGRIDRONE_ANNOTATION_RULES_V1";
    public const string DefaultTeamLabelLimitation =
        "Labels are produced by the AgriDrone team for pilot evaluation and are not expert-confirmed diagnoses.";

    public const string DatasetVersion = DefaultDatasetVersion;
    public const string AnnotationRuleVersion = DefaultAnnotationRuleVersion;
    public const string TeamLabelLimitation = DefaultTeamLabelLimitation;

    public static IReadOnlySet<string> BaselineSuspectedDiseaseCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "BROWN_SPOT",
            "ANTHRACNOSE"
        };

    public static IReadOnlySet<string> BaselineConfusingNonDiseaseCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SUNBURN",
            "MECHANICAL_SCAR"
        };

    public static IReadOnlySet<string> SuspectedDiseaseCodes => BaselineSuspectedDiseaseCodes;
    public static IReadOnlySet<string> ConfusingNonDiseaseCodes => BaselineConfusingNonDiseaseCodes;

    public static bool IsAllowed(string code, PlantHealthLabelKind kind)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Enum.IsDefined(kind);
    }
}
