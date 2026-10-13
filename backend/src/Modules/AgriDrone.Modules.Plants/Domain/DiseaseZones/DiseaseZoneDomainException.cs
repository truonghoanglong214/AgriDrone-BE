namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public sealed class DiseaseZoneDomainException : InvalidOperationException
{
    public DiseaseZoneDomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}

public static class DiseaseZoneDomainErrorCodes
{
    public const string InvalidTransition = "DiseaseZone.InvalidTransition";
    public const string VersionConflict = "DiseaseZone.VersionConflict";
    public const string EmptyMembership = "DiseaseZone.EmptyMembership";
    public const string DuplicateMembership = "DiseaseZone.DuplicateMembership";
    public const string RecommendationDecisionRequired = "DiseaseZone.RecommendationDecisionRequired";
    public const string RecommendationNotApplicable = "DiseaseZone.RecommendationNotApplicable";
    public const string ReplacementMismatch = "DiseaseZone.ReplacementMismatch";
}
