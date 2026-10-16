using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.FinalizeMissionUpload;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Media;

internal static class MissionUploadReadinessPolicy
{
    public static int GetAcceptedMediaCount(
        DroneMission mission,
        MissionUploadReadinessSnapshot snapshot)
    {
        if (mission.Purpose == MissionPurpose.BaselineMapping ||
            mission.MissionType == MissionType.Mapping)
        {
            return snapshot.RawImageCount;
        }

        if (mission.Purpose is MissionPurpose.PlantHealth or
            MissionPurpose.HarvestReadiness ||
            mission.MissionType == MissionType.HealthInspection)
        {
            return snapshot.RawImageCount + snapshot.RawVideoCount;
        }

        return 0;
    }

    public static IReadOnlyList<AppError> Evaluate(
        DroneMission mission,
        MissionUploadReadinessSnapshot snapshot)
    {
        var errors = new List<AppError>();

        if (mission.Status != MissionStatus.Uploading)
        {
            errors.Add(
                FinalizeMissionUploadError.MissionStatusNotAllowed(
                    mission.Status));
        }

        if (snapshot.HasActiveUploadSessions)
        {
            errors.Add(
                FinalizeMissionUploadError.ActiveUploadSessionsExist());
        }

        if (GetAcceptedMediaCount(mission, snapshot) < 1)
        {
            errors.Add(
                FinalizeMissionUploadError.RequiredMediaMissing(
                    mission.Purpose?.ToString() ??
                    mission.MissionType?.ToString() ??
                    "Unknown"));
        }

        if (!snapshot.HasTelemetryImport ||
            snapshot.ImportedPointCount < 2)
        {
            errors.Add(FinalizeMissionUploadError.TelemetryMissing());
        }

        if (snapshot.ImportedPointCount != snapshot.PersistedPointCount)
        {
            errors.Add(
                FinalizeMissionUploadError.TelemetryInconsistent(
                    snapshot.ImportedPointCount,
                    snapshot.PersistedPointCount));
        }

        var route = mission.FlightRoute;

        if (route is null ||
            route.IsEmpty ||
            route.NumPoints < 2 ||
            route.SRID != 4326)
        {
            errors.Add(FinalizeMissionUploadError.FlightRouteMissing());
        }

        return errors.ToArray();
    }
}
