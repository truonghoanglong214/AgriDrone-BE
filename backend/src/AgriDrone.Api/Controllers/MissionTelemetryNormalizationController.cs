using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/missions/{missionId:guid}/farms/{farmId:guid}/telemetry")]
public sealed class MissionTelemetryNormalizationController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    /// <summary>
    /// Giải mã Blackbox TXT/BBL thành các đoạn telemetry để kiểm tra trước import.
    /// </summary>
    [HttpPost("normalize")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20L * 1024 * 1024)]
    public async Task<IResult> Normalize(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromForm] NormalizeTelemetryLogRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Unauthorized();

        var access = await authorizationService.AuthorizeAsync(
            User,
            new FarmAccessTarget(tenantId, farmId),
            AccessAuthorizationPolicies.FarmManage);

        if (!access.Succeeded)
            return Results.Forbid();

        if (request.File is null || request.File.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "A non-empty log file is required.");
        }

        if (request.File.Length > 20L * 1024 * 1024)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: "The log must not exceed 20 MiB.");
        }

        var fileName = Path.GetFileName(
            request.File.FileName.Replace('\\', '/'));

        await using var content = request.File.OpenReadStream();

        var result = await sender.Send(
            new NormalizeTelemetryLogCommand(
                farmId,
                missionId,
                fileName,
                content),
            cancellationToken);

        // API response mapping stays in the API layer.
        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new
            {
                value.SourceFileName,
                value.SourceChecksum,
                value.Warnings,
                Segments = value.Segments.Select(segment => new
                {
                    segment.SegmentIndex,
                    segment.SourcePointCount,
                    segment.RejectedPointCount,
                    PointCount = segment.Points.Count,
                    FirstRecordedAt = segment.Points.Count > 0
                        ? (DateTimeOffset?)segment.Points[0].RecordedAt
                        : null,
                    LastRecordedAt = segment.Points.Count > 0
                        ? (DateTimeOffset?)segment.Points[^1].RecordedAt
                        : null,
                    FitsPointCountLimit =
                        segment.Points.Count is >= 2 and <= 50_000,
                    segment.Warnings,
                    Points = segment.Points.Select(point => new
                    {
                        point.SequenceNumber,
                        point.RecordedAt,
                        point.Longitude,
                        point.Latitude,
                        point.AltitudeM,
                        AltitudeReference =
                            point.AltitudeReference?.ToString(),
                        point.HeadingDeg,
                        point.SpeedMps,
                        point.HorizontalAccuracyM
                    }).ToArray()
                }).ToArray()
            }));
    }
}