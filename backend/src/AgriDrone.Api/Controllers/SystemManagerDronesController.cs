using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemManagerDroneDetails;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/drones")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerDronesController(ISender sender) : ControllerBase
{
    /// <summary>Lấy thông tin chi tiết drone khi SystemManager có quyền trên Farm.</summary>
    [HttpGet("{droneId:guid}")]
    [ProducesResponseType(typeof(DroneRegistryItemResponse), StatusCodes.Status200OK)]
    public async Task<IResult> GetDetails(
        [FromRoute] Guid farmId,
        [FromRoute] Guid droneId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSystemManagerDroneDetailsQuery(farmId, droneId),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Tìm drone còn khả dụng trong khoảng StartAt–EndAt và hỗ trợ Purpose yêu cầu. SystemManager phải có quyền trên Farm; kết quả tìm kiếm không thay thế kiểm tra lại khi đặt lịch hoặc bắt đầu bay.</remarks>
    [HttpGet("available")]
    public async Task<IResult> GetAvailable(
        [FromRoute] Guid farmId,
        [FromQuery] GetAvailableDronesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAvailableDronesQuery(
                farmId,
                request.StartAt,
                request.EndAt,
                request.Purpose),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
