using AgriDrone.Api.Contracts.Farms;
using AgriDrone.SharedKernel.Application.Pagination;
using CreateFarmResult = AgriDrone.Modules.Farms.Application.Features.CreateFarm.CreateFarmResponse;
using GetFarmByIdResult = AgriDrone.Modules.Farms.Application.Features.GetFarmById.GetFarmByIdResponse;
using FarmListItemResult = AgriDrone.Modules.Farms.Application.Features.GetFarm.FarmListItemResponse;
using UpdateFarmDetailResult = AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail.UpdateFarmDetailResponse;
using ArchivedFarmResult = AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms.ArchivedFarmResponse;

namespace AgriDrone.Api.Mapping;

internal static class FarmResponseMapper
{
    public static CreateFarmApiResponse ToResponse(CreateFarmResult farm) =>
        new(
            farm.farmId,
            farm.tenantId,
            farm.code,
            farm.name,
            farm.address,
            GeoJsonGeometryMapper.FromPolygon(farm.boundary),
            GeoJsonGeometryMapper.FromPoint(farm.centerPoint),
            farm.areaHectares,
            farm.status,
            farm.createdBy,
            farm.createdAt);

    public static GetFarmByIdResponse ToResponse(GetFarmByIdResult farm) =>
        new(
            farm.id,
            farm.tenantId,
            farm.code,
            farm.name,
            farm.address,
            GeoJsonGeometryMapper.FromPolygon(farm.boundary),
            GeoJsonGeometryMapper.FromPoint(farm.centerPoint),
            farm.areaHectares,
            farm.status,
            farm.createdAt,
            farm.createdBy,
            farm.updatedAt,
            farm.version);

    public static UpdateFarmDetailResponse ToResponse(UpdateFarmDetailResult farm) =>
        new(
            farm.id,
            farm.tenantId,
            farm.code,
            farm.name,
            farm.address,
            GeoJsonGeometryMapper.FromPolygon(farm.boundary),
            GeoJsonGeometryMapper.FromPoint(farm.centerPoint),
            farm.areaHectares,
            farm.status,
            farm.updatedAt,
            farm.version);

    public static ArchivedFarmApiResponse ToResponse(ArchivedFarmResult farm) =>
        new(
            farm.Id,
            farm.TenantId,
            farm.Code,
            farm.Name,
            farm.Address,
            GeoJsonGeometryMapper.FromPolygon(farm.Boundary),
            GeoJsonGeometryMapper.FromPoint(farm.CenterPoint),
            farm.AreaHectares,
            farm.Status,
            farm.CreatedAt,
            farm.CreatedBy,
            farm.UpdatedAt,
            farm.ArchivedAt,
            farm.Version);

    public static PagedResult<ArchivedFarmApiResponse> ToResponse(
        PagedResult<ArchivedFarmResult> farms) =>
        new(
            farms.Items.Select(ToResponse).ToArray(),
            farms.PageNumber,
            farms.PageSize,
            farms.TotalCount);

    public static PagedResult<FarmListItemApiResponse> ToResponse(
        PagedResult<FarmListItemResult> farms) =>
        new(
            farms.Items.Select(ToResponse).ToArray(),
            farms.PageNumber,
            farms.PageSize,
            farms.TotalCount);

    private static FarmListItemApiResponse ToResponse(FarmListItemResult farm) =>
        new(
            farm.id,
            farm.tenantId,
            farm.code,
            farm.name,
            farm.address,
            GeoJsonGeometryMapper.FromPolygon(farm.boundary),
            GeoJsonGeometryMapper.FromPoint(farm.centerPoint),
            farm.areaHectares,
            farm.status,
            farm.createdAt,
            farm.createdBy);
}
