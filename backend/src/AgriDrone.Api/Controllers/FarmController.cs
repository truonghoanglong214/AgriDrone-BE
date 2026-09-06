using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Farms.Application.Features.GetFarm;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using AgriDrone.Modules.Farms.Application.Features.CreateFarm;
using AgriDrone.Modules.Identity.Application.Features.RegisterUser;
using AgriDrone.Modules.Farms.Application.Features.GetFarmById;
using AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;
using AgriDrone.Api.Contracts.Zones;
using AgriDrone.Modules.Farms.Application.Features.CreateZone;
using AgriDrone.Modules.Farms.Application.Features.GetZoneById;
using AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

namespace AgriDrone.Api.Controllers
{
    [Route("api/farms")]
    [ApiController]
    public class FarmController(
        ISender sender) : ControllerBase
    {
        /// <summary>Lấy danh sách farm của tenant hiện tại.</summary>
        /// <remarks>
        /// Trả danh sách farm phân trang trong tenant từ access token. Chỉ Tenant
        /// Admin hoặc Owner được phép xem catalog farm qua endpoint này.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> GetFarms(
            [FromQuery] GetFarmsRequest request,
            CancellationToken cancellationToken)
        {
            var command = new GetFarmQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farms => Results.Ok(FarmResponseMapper.ToResponse(farms)));
        }

        /// <summary>Tạo farm trong tenant hiện tại.</summary>
        /// <remarks>
        /// Tenant Admin hoặc Owner tạo farm với mã duy nhất trong tenant, thông
        /// tin vị trí và boundary GeoJSON sử dụng hệ tọa độ WGS84 (SRID 4326).
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> CreateFarm(
            [FromBody] CreateFarmRequest request,
            CancellationToken cancellationToken)
        {
            var command =  new CreateFarmCommand(
                request.Code,
                request.Name,
                request.Address,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                GeoJsonGeometryMapper.ToPoint(request.CenterPoint),
                request.AreaHectares);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }

        /// <summary>Lấy chi tiết farm.</summary>
        /// <remarks>
        /// Trả thông tin farm thuộc tenant hiện tại khi người dùng có quyền đọc
        /// farm thông qua tenant ownership hoặc farm membership.
        /// </remarks>
        [HttpGet("{farmId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantMember)]
        public async Task<IResult> GetFarmById(
            [FromRoute] GetFarmByIdRequest request,
            CancellationToken cancellationToken)
        {
            var command = new GetFarmByIdCommand(request.FarmId);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }

        /// <summary>Tạo zone trong farm.</summary>
        /// <remarks>
        /// Tenant Owner hoặc Farm Manager được assign tạo zone với mã duy nhất
        /// trong farm. Boundary GeoJSON, nếu có, phải là Polygon SRID 4326 hợp lệ.
        /// Zone mới được kích hoạt và bắt đầu ở version 1.
        /// </remarks>
        [HttpPost("{farmId:guid}/zones")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantMember)]
        public async Task<IResult> CreateZone(
            [FromRoute] Guid farmId,
            [FromBody] CreateZoneRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateZoneCommand(
                farmId,
                request.Code,
                request.Name,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                request.AreaHectares);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                zone => Results.Created(
                    $"/api/farms/{farmId}/zones/{zone.ZoneId}",
                    FarmZoneResponseMapper.ToResponse(zone)));
        }

        /// <summary>Lấy danh sách zone theo farm.</summary>
        /// <remarks>
        /// Trả các zone chưa archive thuộc farm và tenant hiện tại. Kết quả được
        /// lọc theo FarmAccessScope và ZoneAssignment đang hoạt động của người dùng.
        /// </remarks>
        [HttpGet("{farmId:guid}/zones")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantMember)]
        public async Task<IResult> GetZonesByFarm(
            [FromRoute] Guid farmId,
            CancellationToken cancellationToken)
        {
            var query = new GetZonesByFarmQuery(farmId);

            var result = await sender.Send(query, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                zones => Results.Ok(FarmZoneResponseMapper.ToResponse(zones)));
        }

        /// <summary>Lấy chi tiết zone.</summary>
        /// <remarks>
        /// Trả zone thuộc đúng farm và tenant hiện tại khi người dùng có quyền
        /// qua FarmAccessScope hoặc ZoneAssignment. Response bao gồm version phục
        /// vụ optimistic concurrency cho các thao tác cập nhật sau này.
        /// </remarks>
        [HttpGet("{farmId:guid}/zones/{zoneId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantMember)]
        public async Task<IResult> GetZoneById(
            [FromRoute] Guid farmId,
            [FromRoute] Guid zoneId,
            CancellationToken cancellationToken)
        {
            var query = new GetZoneByIdQuery(farmId, zoneId);

            var result = await sender.Send(query, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                zone => Results.Ok(FarmZoneResponseMapper.ToResponse(zone)));
        }

        /// <summary>Cập nhật thông tin farm.</summary>
        /// <remarks>
        /// Tenant Admin hoặc Owner cập nhật tên, địa chỉ, vị trí, boundary và
        /// diện tích farm. ExpectedVersion được dùng để ngăn ghi đè cập nhật đồng thời.
        /// </remarks>
        [HttpPut("{farmId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> UpdateFarmDetail(
            [FromRoute] Guid farmId,
            [FromBody] UpdateFarmDetailRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateFarmDetailCommand(
                farmId,
                request.Name,
                request.Address,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                GeoJsonGeometryMapper.ToPoint(request.CenterPoint),
                request.AreaHectares,
                request.ExpectedVersion);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }
    }
}
