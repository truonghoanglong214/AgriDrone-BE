namespace AgriDrone.Modules.Farms.Domain.Boundaries;

public sealed class FarmBoundaryDomainException : InvalidOperationException
{
    public FarmBoundaryDomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}

public static class FarmBoundaryDomainErrorCodes
{
    public const string InvalidTransition = "FarmBoundary.InvalidTransition";
    public const string VersionConflict = "FarmBoundary.VersionConflict";
    public const string ZoneOutsideBoundary = "FarmBoundary.ZoneOutsideBoundary";
    public const string ZoneBoundaryRequired = "FarmBoundary.ZoneBoundaryRequired";
    public const string ZonesOverlap = "FarmBoundary.ZonesOverlap";
    public const string ReplacementMismatch = "FarmBoundary.ReplacementMismatch";
    public const string CorrectedPositionRequired = "BoundaryException.CorrectedPositionRequired";
    public const string CorrectedPositionNotAllowed = "BoundaryException.CorrectedPositionNotAllowed";
}
