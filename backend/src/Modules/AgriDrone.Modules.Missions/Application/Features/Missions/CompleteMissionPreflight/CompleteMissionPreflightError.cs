using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

internal static class CompleteMissionPreflightError
{
    public static AppError StaleChecklistDefinition() =>
        AppError.Conflict(
            "MissionPreflight.StaleChecklistDefinition",
            "The checklist definition is no longer active. Load the current checklist and complete it again.");
    public static AppError FarmAccessDenied() =>
        AppError.Forbidden(
            "MissionPreflight.FarmAccessDenied",
            "The current SystemManager is not assigned and flight-qualified for this Farm.");

    public static AppError MissionNotOrderBound() =>
        AppError.Conflict(
            "MissionPreflight.MissionNotOrderBound",
            "Pre-flight completion is only available for order-bound Missions.");

    public static AppError InvalidStatus(MissionStatus status) =>
        AppError.Conflict(
            "MissionPreflight.InvalidStatus",
            $"Mission in status '{status}' cannot accept a pre-flight checklist.");

    public static AppError OperationPayloadConflict(Guid operationId) =>
        AppError.Conflict(
            "MissionPreflight.OperationPayloadConflict",
            $"Pre-flight operation '{operationId}' was already used with another payload.");

    public static AppError AlreadyCompleted() =>
        AppError.Conflict(
            "MissionPreflight.AlreadyCompleted",
            "The completed pre-flight checklist snapshot is immutable.");
}
