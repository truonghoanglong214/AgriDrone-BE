using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.FinalizeMissionUpload;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Media;

internal static class MissionUploadReadinessPolicy
{
    public static int GetAcceptedMediaCount(
        MissionType missionType,
        MissionUploadReadinessSnapshot snapshot)
    {
        return missionType switch
        {
            MissionType.Mapping => snapshot.RawImageCount,
            MissionType.HealthInspection =>
                snapshot.RawImageCount + snapshot.RawVideoCount,
            _ => 0
        };
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

        if (GetAcceptedMediaCount(mission.MissionType, snapshot) < 1)
        {
            errors.Add(
                FinalizeMissionUploadError.RequiredMediaMissing(
                    mission.MissionType));
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