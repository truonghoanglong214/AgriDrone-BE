using AgriDrone.Api.Contracts.Zones;
using CreateZoneResult = AgriDrone.Modules.Farms.Application.Features.CreateZone.CreateZoneResponse;
using GetZoneByIdResult = AgriDrone.Modules.Farms.Application.Features.GetZoneById.GetZoneByIdResponse;
using ZoneListItemResult = AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm.ZoneListItemResponse;

namespace AgriDrone.Api.Mapping;

internal static class FarmZoneResponseMapper
{
    public static CreateZoneApiResponse ToResponse(CreateZoneResult zone) =>
        new(
            zone.ZoneId,
            zone.FarmId,
            zone.Code,
            zone.Name,
            GeoJsonGeometryMapper.FromPolygon(zone.Boundary),
            zone.AreaHectares,
            zone.Status,
            zone.Version,
            zone.CreatedAt,
            zone.CreatedBy);

    public static IReadOnlyList<ZoneListItemApiResponse> ToResponse(
        IReadOnlyList<ZoneListItemResult> zones) =>
        zones.Select(ToResponse).ToArray();

    public static GetZoneByIdApiResponse ToResponse(GetZoneByIdResult zone) =>
        new(
            zone.ZoneId,
            zone.FarmId,
            zone.Code,
            zone.Name,
            GeoJsonGeometryMapper.FromPolygon(zone.Boundary),
            zone.AreaHectares,
            zone.Status,
            zone.Version,
            zone.CreatedAt,
            zone.CreatedBy,
            zone.UpdatedAt);

    private static ZoneListItemApiResponse ToResponse(ZoneListItemResult zone) =>
        new(
            zone.ZoneId,
            zone.FarmId,
            zone.Code,
            zone.Name,
            GeoJsonGeometryMapper.FromPolygon(zone.Boundary),
            zone.AreaHectares,
            zone.Status,
            zone.Version,
            zone.CreatedAt);
}
