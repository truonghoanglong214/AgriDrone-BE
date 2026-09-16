using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal static class MediaReadErrors
{
    public static AppError NotFound() => AppError.NotFound(
        "MissionMedia.NotFound", "An active media asset was not found in this mission.");
}
