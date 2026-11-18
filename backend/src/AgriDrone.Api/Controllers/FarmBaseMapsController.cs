using AgriDrone.Modules.Farms.Application.Features.Maps.CompareFarmBaseMaps;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapAnalytics;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/farms/{farmId:guid}/base-maps")]
[Authorize]
public sealed class FarmBaseMapsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// API 1: Lấy danh sách lịch sử tất cả các phiên bản bản đồ nền của trang trại.
    /// </summary>
    /// <remarks>
    /// Trả về danh sách tóm tắt các đợt bay map (version 1, version 2...), ngày bay, mã chuyến bay,
    /// tổng số trụ chốt, thông số hàng/cây và link ảnh xem trước map_preview.jpg từ MinIO.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FarmBaseMapSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBaseMaps(
        [FromRoute] Guid farmId,
        [FromQuery] bool includeDraft = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFarmBaseMapsQuery(farmId, includeDraft);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// API 3: So sánh biến động cây trồng giữa 2 phiên bản bản đồ (Map Diff / Comparison).
    /// </summary>
    /// <remarks>
    /// So sánh giữa 2 phiên bản bản đồ (mặc định lấy 2 phiên bản gần nhất nếu không truyền param):
    /// - Tổng số trụ trước/sau, chênh lệch ròng (+/-).
    /// - Thống kê số trụ mới trồng thêm (CREATED), trụ dời vị trí (RELOCATED), trụ thay thế (REPLACED), trụ nhổ bỏ (REMOVED).
    /// - Danh sách tọa độ chi tiết của từng trụ có biến động để render lên Leaflet so sánh.
    /// </remarks>
    [HttpGet("compare")]
    [ProducesResponseType<CompareFarmBaseMapsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> CompareBaseMaps(
        [FromRoute] Guid farmId,
        [FromQuery] int? fromVersion = null,
        [FromQuery] int? toVersion = null,
        [FromQuery] Guid? fromVersionId = null,
        [FromQuery] Guid? toVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new CompareFarmBaseMapsQuery(
            FarmId: farmId,
            FromVersionNumber: fromVersion,
            ToVersionNumber: toVersion,
            FromVersionId: fromVersionId,
            ToVersionId: toVersionId);

        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// API 4: Thống kê tăng trưởng và biến động quy mô trang trại qua các thời kỳ (Analytics/Chart).
    /// </summary>
    /// <remarks>
    /// Cung cấp dữ liệu để vẽ biểu đồ Dashboard:
    /// - Số lượng trụ qua từng phiên bản bản đồ và mức chênh lệch delta (+/-).
    /// - Mật độ trụ cây trên mỗi hecta (poles/ha) dựa theo ranh giới trang trại đã duyệt.
    /// </remarks>
    [HttpGet("analytics")]
    [ProducesResponseType<FarmBaseMapAnalyticsResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> GetBaseMapAnalytics(
        [FromRoute] Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFarmBaseMapAnalyticsQuery(farmId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// API 2: Lấy chi tiết một phiên bản bản đồ cụ thể kèm toàn bộ trụ cây để render lên Leaflet.
    /// </summary>
    /// <remarks>
    /// Cung cấp đầy đủ:
    /// - Ranh giới trang trại (Boundary GeoJSON).
    /// - Tọa độ bao quát 4 góc (Bounds) để zoom vừa khung hình Leaflet.
    /// - Link ảnh trực giao xem trước (map_preview.jpg).
    /// - Danh sách toàn bộ trụ cây thuộc phiên bản này (Row, Col, Lat, Lon, Trạng thái).
    /// </remarks>
    [HttpGet("{versionId:guid}")]
    [ProducesResponseType<FarmBaseMapDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBaseMapDetail(
        [FromRoute] Guid farmId,
        [FromRoute] Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFarmBaseMapDetailQuery(farmId, versionId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}

