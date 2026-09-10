using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal interface IMissionMediaRepository
{
    void Add(
        MediaAsset mediaAsset,
        MissionMedia missionMedia);
}