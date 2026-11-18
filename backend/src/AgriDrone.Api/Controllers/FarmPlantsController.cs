using AgriDrone.Modules.Plants.Application.Features.GetPlantLifecycleHistory;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/plants")]
[Authorize]
public sealed class FarmPlantsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// API 5: Lấy toàn bộ lịch sử vòng đời và các sự kiện biến động của 1 trụ cây cụ thể.
    /// </summary>
    /// <remarks>
    /// Khi người dùng click vào một trụ cây trên bản đồ:
    /// - Hiển thị mã trụ (PlantCode), vị trí hiện tại (Row, Col, Lat, Lon).
    /// - Timeline lịch sử: Ngày khởi tạo từ bản đồ cơ sở, các lần nắn chỉnh tọa độ (dời bao nhiêu mét),
    ///   thay thế, hoặc các sự kiện biến động qua từng đợt bay mission.
    /// </remarks>
    [HttpGet("{plantId:guid}/lifecycle")]
    [ProducesResponseType<PlantLifecycleHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetPlantLifecycle(
        [FromRoute] Guid farmId,
        [FromRoute] Guid plantId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPlantLifecycleHistoryQuery(farmId, plantId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}

