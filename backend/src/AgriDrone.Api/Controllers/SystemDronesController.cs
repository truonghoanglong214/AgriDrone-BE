using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetSystemDroneDetails;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetSystemDrones;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/drones")]
[Authorize(
    Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemDronesController(
    ISender sender)
    : ControllerBase
{
    /// <summary>Lấy danh sách Drone trên toàn hệ thống.</summary>
    /// <remarks>
    /// System Admin theo dõi Drone xuyên tenant để hỗ trợ vận hành. Có thể lọc
    /// theo TenantId, trạng thái, từ khóa và phân trang. API này chỉ cung cấp khả
    /// năng quan sát, không thay đổi quyền sở hữu hoặc trạng thái Drone.
    /// </remarks>
    [HttpGet]
    public async Task<IResult> GetDrones(
        [FromQuery] GetSystemDronesRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetSystemDronesQuery(
            TenantId: request.TenantId,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            Status: request.Status,
            Search: request.Search);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drones => Results.Ok(drones));
    }

    /// <summary>Lấy chi tiết một Drone trên toàn hệ thống.</summary>
    /// <remarks>
    /// System Admin xem chi tiết Drone không phụ thuộc tenant nhằm điều tra và hỗ
    /// trợ kỹ thuật. API chỉ đọc và không cho phép sửa thông tin, chuyển tenant
    /// hoặc thay đổi trạng thái vận hành của Drone.
    /// </remarks>
    [HttpGet("{droneId:guid}")]
    public async Task<IResult> GetDroneDetails(
        Guid droneId,
        CancellationToken cancellationToken)
    {
        var query =
            new GetSystemDroneDetailsQuery(droneId);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drone => Results.Ok(drone));
    }
}
