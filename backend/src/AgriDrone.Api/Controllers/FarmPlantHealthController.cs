using AgriDrone.Modules.Missions.Application.Features.Processing.GetFarmPlantHealthMap;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/plant-health-map")]
public sealed class FarmPlantHealthController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lấy bản đồ nền và lịch sử nhận diện sức khỏe cây (bệnh và quả) của toàn bộ trang trại.
    /// </summary>
    /// <remarks>
    /// API cung cấp đầy đủ dữ liệu cho Frontend render bản đồ Leaflet:
    /// - BaseMap: Chuyến bay ghép bản đồ (source_mission_id), link ảnh map_preview.jpg từ MinIO, tọa độ bounds.
    /// - Summary: Tổng số trụ, số trụ bình thường (34), số trụ có trái khỏe (11), số trụ nhiễm bệnh (16).
    /// - Plants: Danh sách toàn bộ trụ cây phân loại theo 3 nhóm màu, kèm danh sách vết nhận diện và link ảnh crop MinIO.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<FarmPlantHealthMapResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetPlantHealthMap(
        [FromRoute] Guid farmId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var query = new GetFarmPlantHealthMapQuery(farmId, tenantId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}

