using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application
    .Features.Telemetry.ImportTelemetry;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionTelemetryController(
    ISender sender,
    ISystemManagerAccessService managerAccessService)
    : ControllerBase
{
    /// <summary>Nhập telemetry đã chuẩn hóa để tạo đường bay thực tế.</summary>
    /// <remarks>
    /// Nhận một hoặc nhiều segment đã decode, tổng cộng từ 2 đến 50.000 điểm.
    /// Backend gộp điểm và sắp xếp theo RecordedAt UTC, sau đó đánh lại
    /// SequenceNumber liên tục. Các điểm phải nằm trong khoảng StartedAt đến
    /// EndedAt của Mission FlightCompleted hoặc Uploading. Import đầu tiên từ
    /// FlightCompleted sẽ chuyển Mission sang Uploading. Mỗi Mission giữ một telemetry import;
    /// request có Idempotency-Key header có thể giữ nguyên khi retry cùng payload;
    /// cùng payload sẽ trả kết quả cũ, payload khác sẽ thay import hiện tại.
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/telemetry/imports")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> ImportTelemetry(
        [FromRoute] Guid farmId,
        Guid missionId,
        [FromBody] ImportMissionTelemetryRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty UUID idempotency key is required."]
            });
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var segments = request.Segments?
            .Select(segment => new ImportTelemetrySegment(
                segment.SegmentIndex,
                segment.Points?
                    .Select(point => new ImportTelemetryPoint(
                        point.SequenceNumber,
                        point.RecordedAt,
                        point.Longitude,
                        point.Latitude,
                        point.AltitudeM,
                        point.AltitudeReference,
                        point.HeadingDeg,
                        point.SpeedMps,
                        point.HorizontalAccuracyM))
                    .ToArray()
                    ?? []))
            .ToArray()
            ?? [];

        var command = new ImportTelemetryCommand(
            tenantId,
            farmId,
            missionId,
            idempotencyKey,
            request.SourceFileName,
            request.SourceChecksum,
            request.ExpectedMissionVersion,
            segments);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new ImportMissionTelemetryResponse(
                    value.TelemetryImportId,
                    value.MissionId,
                    value.PointCount,
                    value.FirstRecordedAt,
                    value.LastRecordedAt,
                    value.MissionVersion,
                    value.ReusedOperation)));
    }
}
