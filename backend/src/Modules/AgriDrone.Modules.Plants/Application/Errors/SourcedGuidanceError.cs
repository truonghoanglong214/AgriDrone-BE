using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Plants.Application.Errors;

public static class SourcedGuidanceError
{
    public static AppError NotFound() => AppError.NotFound(
        "SourcedGuidance.NotFound", "Sourced guidance was not found.");

    public static AppError InvalidSuspectedClass() => AppError.Validation(
        "SourcedGuidance.InvalidSuspectedClass",
        "Guidance must reference an active team-labelled suspected-disease class.");

    public static AppError VersionConflict() => AppError.Conflict(
        "SourcedGuidance.VersionConflict",
        "Sourced guidance was changed by another request.");

    public static AppError EffectiveWindowOverlap() => AppError.Conflict(
        "SourcedGuidance.EffectiveWindowOverlap",
        "Another active guidance version overlaps this effective window.");

    public static AppError InvalidTransition(string message) => AppError.Conflict(
        "SourcedGuidance.InvalidTransition", message);

    public static AppError Forbidden() => AppError.Forbidden(
        "SourcedGuidance.Forbidden", "System administrator access is required.");
}
