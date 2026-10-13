namespace AgriDrone.Modules.Surveys.Domain;

public sealed class HarvestReadinessCriterionDomainException(
    string code,
    string message) : InvalidOperationException(message)
{
    public string Code { get; } =
        string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException(
                "Error code is required.",
                nameof(code))
            : code;
}

public static class HarvestReadinessCriterionErrorCodes
{
    public const string InvalidLifecycleTransition =
        "HarvestReadinessCriterion.InvalidLifecycleTransition";
    public const string VersionConflict =
        "HarvestReadinessCriterion.VersionConflict";
    public const string ValidationEvidenceRequired =
        "HarvestReadinessCriterion.ValidationEvidenceRequired";
    public const string CriterionNotAvailable =
        "HarvestReadinessCriterion.NotAvailable";
}
