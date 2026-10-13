namespace AgriDrone.Modules.Plants.Domain.Recommendations;

public sealed class TreatmentRecommendationDomainException : InvalidOperationException
{
    public TreatmentRecommendationDomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}

public static class TreatmentRecommendationDomainErrorCodes
{
    public const string InvalidTransition = "TreatmentRecommendation.InvalidTransition";
    public const string VersionConflict = "TreatmentRecommendation.VersionConflict";
    public const string ReplacementMismatch = "TreatmentRecommendation.ReplacementMismatch";
}
