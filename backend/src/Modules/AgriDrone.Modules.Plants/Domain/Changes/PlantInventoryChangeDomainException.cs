namespace AgriDrone.Modules.Plants.Domain.Changes;

public sealed class PlantInventoryChangeDomainException : InvalidOperationException
{
    public PlantInventoryChangeDomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}

public static class PlantInventoryChangeDomainErrorCodes
{
    public const string InvalidTransition = "PlantInventoryChange.InvalidTransition";
    public const string VersionConflict = "PlantInventoryChange.VersionConflict";
    public const string ExistingPlantRequired = "PlantInventoryChange.ExistingPlantRequired";
    public const string ExistingPlantNotAllowed = "PlantInventoryChange.ExistingPlantNotAllowed";
    public const string SurveyEvidenceRequired = "PlantInventoryChange.SurveyEvidenceRequired";
    public const string SurveyEvidenceNotAllowed = "PlantInventoryChange.SurveyEvidenceNotAllowed";
    public const string ResultingPlantRequired = "PlantInventoryChange.ResultingPlantRequired";
    public const string ResultingPlantNotAllowed = "PlantInventoryChange.ResultingPlantNotAllowed";
    public const string ReplacementPlantMustDiffer = "PlantInventoryChange.ReplacementPlantMustDiffer";
    public const string ReporterMismatch = "PlantInventoryChange.ReporterMismatch";
}
