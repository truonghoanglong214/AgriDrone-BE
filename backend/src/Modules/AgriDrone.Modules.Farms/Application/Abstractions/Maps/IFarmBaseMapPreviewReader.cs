using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Maps;

public interface IFarmBaseMapPreviewReader
{
    Task<FarmBaseMapPreview?> GetAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        CancellationToken cancellationToken = default);
}

public sealed record FarmBaseMapPreview(string Url, MapBoundsDto? Bounds);
