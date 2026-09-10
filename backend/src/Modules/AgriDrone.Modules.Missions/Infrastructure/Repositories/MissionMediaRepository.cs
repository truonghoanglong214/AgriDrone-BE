using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class MissionMediaRepository(
    MissionsDbContext dbContext)
    : IMissionMediaRepository
{
    public void Add(
        MediaAsset mediaAsset,
        MissionMedia missionMedia)
    {
        ArgumentNullException.ThrowIfNull(mediaAsset);
        ArgumentNullException.ThrowIfNull(missionMedia);

        dbContext.MediaAssets.Add(mediaAsset);
        dbContext.MissionMedia.Add(missionMedia);
    }
}