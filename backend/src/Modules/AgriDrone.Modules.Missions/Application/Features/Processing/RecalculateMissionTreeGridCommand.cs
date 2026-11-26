using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

/// <summary>
/// Chuẩn hóa lại Hàng &amp; Cột cho danh sách cây đã nhận diện (đã sửa tay trên FE) mà không cần chạy YOLO lại.
/// </summary>
public sealed record RecalculateMissionTreeGridCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    IReadOnlyList<TreeDetectionItem> ActiveTrees,
    double RowSpacing = 2.8,
    double ColSpacing = 2.5,
    double? AnchorLat = null,
    double? AnchorLon = null,
    double? CustomAngleDeg = null,
    object? PolygonGeoJson = null) : IRequest<Result<DetectMissionTreesResult>>;
