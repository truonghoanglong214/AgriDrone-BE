using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Farms.Application.Features.GetFarm;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.Api.Legacy;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using AgriDrone.Modules.Farms.Application.Features.CreateFarm;
using AgriDrone.Modules.Farms.Application.Features.GetFarmById;
using AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;
using AgriDrone.Modules.Farms.Application.Features.UpdateZone;
using AgriDrone.Modules.Farms.Application.Features.ArchiveFarm;
using AgriDrone.Modules.Farms.Application.Features.ArchiveZone;
using AgriDrone.Api.Contracts.Zones;
using AgriDrone.Modules.Farms.Application.Features.CreateZone;
using AgriDrone.Modules.Farms.Application.Features.GetZoneById;
using AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;
using AgriDrone.Modules.Farms.Application.Features.RestoreFarm;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarmById;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;

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

        /// <summary>Lấy danh sách farm đã archive của tenant hiện tại.</summary>
        /// <remarks>
        /// Chỉ Tenant Owner được phép xem danh sách. Response bao gồm thời điểm
        /// archive và version hiện tại để phục vụ thao tác restore an toàn.
        /// </remarks>
        [HttpGet("archived")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
        public async Task<IResult> GetArchivedFarms(
            [FromQuery] GetFarmsRequest request,
            CancellationToken cancellationToken)
        {
            var query = new GetArchivedFarmsQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(query, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farms => Results.Ok(FarmResponseMapper.ToResponse(farms)));
        }

        /// <summary>Lấy chi tiết một farm đã archive.</summary>
        /// <remarks>
        /// Chỉ Tenant Owner được phép xem. Farm phải thuộc tenant hiện tại và
        /// đang ở trạng thái archived; nếu không, API trả về 404.
        /// </remarks>
        [HttpGet("{farmId:guid}/archived")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
        public async Task<IResult> GetArchivedFarmById(
            [FromRoute] Guid farmId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetArchivedFarmByIdQuery(farmId),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }

        /// <summary>Tạo farm trong tenant hiện tại.</summary>
        /// <remarks>
        /// Tenant Admin hoặc Owner tạo farm với mã duy nhất trong tenant, thông
        /// tin vị trí và boundary GeoJSON sử dụng hệ tọa độ WGS84 (SRID 4326).
        /// </remarks>
        [HttpPost]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        [LegacyEndpoint(
            "farms.create-direct",
            "Submit an Existing Tenant New Farm Survey Request and wait for SystemAdmin approval.")]
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
        /// SystemManager đang là primary manager của Farm tạo zone với mã duy nhất
        /// trong farm. Boundary GeoJSON, nếu có, phải là Polygon SRID 4326 hợp lệ,
        /// nằm trong Farm và không chồng lấn Zone đang hoạt động.
        /// Zone mới được kích hoạt và bắt đầu ở version 1.
        /// </remarks>
        [HttpPost("{farmId:guid}/zones")]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
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

        /// <summary>Cập nhật thông tin một zone.</summary>
        /// <remarks>
        /// SystemManager đang là primary manager của Farm được cập nhật tên,
        /// boundary và diện tích. ExpectedVersion ngăn ghi đè thay đổi đồng thời.
        /// </remarks>
        [HttpPut("{farmId:guid}/zones/{zoneId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
        public async Task<IResult> UpdateZone(
            [FromRoute] Guid farmId,
            [FromRoute] Guid zoneId,
            [FromBody] UpdateZoneRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateZoneCommand(
                farmId,
                zoneId,
                request.Name,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                request.AreaHectares,
                request.ExpectedVersion);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                zone => Results.Ok(FarmZoneResponseMapper.ToResponse(zone)));
        }

        /// <summary>Archive một zone và giữ nguyên lịch sử liên quan.</summary>
        /// <remarks>
        /// SystemManager đang là primary manager của Farm được phép thực hiện.
        /// Yêu cầu bị từ chối nếu Zone còn Mission đang hoạt động.
        /// </remarks>
        [HttpPut("{farmId:guid}/zones/{zoneId:guid}/archive")]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
        public async Task<IResult> ArchiveZone(
            [FromRoute] Guid farmId,
            [FromRoute] Guid zoneId,
            [FromBody] ArchiveZoneRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ArchiveZoneCommand(
                    farmId,
                    zoneId,
                    request.ExpectedVersion),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        /// <summary>Archive một farm và giữ nguyên lịch sử liên quan.</summary>
        /// <remarks>
        /// SystemManager đang là primary manager được phép thực hiện. Farm chỉ
        /// được archive khi không còn Zone hoặc Mission đang hoạt động.
        /// </remarks>
        [HttpPut("{farmId:guid}/archive")]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
        public async Task<IResult> ArchiveFarm(
            [FromRoute] Guid farmId,
            [FromBody] ArchiveFarmRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new ArchiveFarmCommand(
                    farmId,
                    request.ExpectedVersion),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        /// <summary>Restore một farm và giữ nguyên lịch sử liên quan.</summary>
        /// <remarks>
        /// Chỉ Tenant Owner được phép thực hiện. Farm chỉ được restore khi Farm đang ở trạng thái archived.
        /// </remarks>
        [HttpPut("{farmId:guid}/restore")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
        [LegacyEndpoint(
            "farms.restore-direct",
            "Submit the appropriate Survey Request; direct Farm restoration is outside the MVP contract.")]
        public async Task<IResult> RestoreFarm(
            [FromRoute] Guid farmId,
            [FromBody] RestoreFarmRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new RestoreFarmCommand(
                    farmId,
                    request.ExpectedVersion),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        /// <summary>Cập nhật thông tin farm.</summary>
        /// <remarks>
        /// SystemManager đang là primary manager cập nhật tên, địa chỉ, vị trí,
        /// boundary và diện tích Farm. ExpectedVersion ngăn ghi đè đồng thời.
        /// </remarks>
        [HttpPut("{farmId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
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
