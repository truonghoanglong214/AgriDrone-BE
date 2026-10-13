using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyBoundaryVerificationError
{
    public const string InvalidGeometryCode =
        "SurveyBoundary.InvalidGeometry";
    public const string BoundaryVersionConflictCode =
        "SurveyBoundary.VersionConflict";
    public const string BoundaryVersionRequiredCode =
        "SurveyBoundary.ExpectedVersionRequired";
    public const string UnexpectedBoundaryVersionCode =
        "SurveyBoundary.UnexpectedExpectedVersion";
    public const string ZoneBoundaryRequiredCode =
        "SurveyBoundary.ZoneBoundaryRequired";
    public const string ZoneOutsideBoundaryCode =
        "SurveyBoundary.ZoneOutsideBoundary";
    public const string ZonesOverlapCode =
        "SurveyBoundary.ZonesOverlap";
    public const string FarmInventoryIncompleteCode =
        "SurveyBoundary.FarmInventoryIncomplete";
    public const string PersistenceConflictCode =
        "SurveyBoundary.PersistenceConflict";

    public static AppError InvalidGeometry(string description) =>
        AppError.Validation(InvalidGeometryCode, description);

    public static AppError BoundaryVersionConflict() =>
        AppError.Conflict(
            BoundaryVersionConflictCode,
            "The current FarmBoundary changed before verification completed.");

    public static AppError BoundaryVersionRequired() =>
        AppError.Conflict(
            BoundaryVersionRequiredCode,
            "ExpectedCurrentBoundaryVersionNumber is required when the Farm already has an approved boundary.");

    public static AppError UnexpectedBoundaryVersion() =>
        AppError.Conflict(
            UnexpectedBoundaryVersionCode,
            "ExpectedCurrentBoundaryVersionNumber must be omitted when the Farm has no approved boundary.");

    public static AppError ZoneBoundaryRequired() =>
        AppError.Validation(
            ZoneBoundaryRequiredCode,
            "Every active Zone must have a boundary before FarmBoundary approval.");

    public static AppError ZoneOutsideBoundary() =>
        AppError.Validation(
            ZoneOutsideBoundaryCode,
            "Every active Zone must be covered by the approved FarmBoundary.");

    public static AppError ZonesOverlap() =>
        AppError.Validation(
            ZonesOverlapCode,
            "Active Zone boundaries cannot overlap.");

    public static AppError FarmInventoryIncomplete() =>
        AppError.Conflict(
            FarmInventoryIncompleteCode,
            "The Farm has a published base map without an authoritative confirmed pole count.");

    public static AppError PersistenceConflict() =>
        AppError.Conflict(
            PersistenceConflictCode,
            "Boundary verification conflicted with another operation.");
}
