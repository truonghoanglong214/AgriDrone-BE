using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application
    .Features.Telemetry.ImportTelemetry;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionTelemetryController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    [HttpPost(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/telemetry/imports")]
    public async Task<IResult> ImportTelemetry(
        Guid farmId,
        Guid missionId,
        [FromBody] ImportMissionTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var authorization =
            await authorizationService.AuthorizeAsync(
                User,
                new FarmAccessTarget(
                    tenantId,
                    farmId),
                AccessAuthorizationPolicies.FarmManage);

        if (!authorization.Succeeded)
        {
            return Results.Forbid();
        }

        var points = request.Points?
            .Select(point =>
                new ImportTelemetryPoint(
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
            ?? [];

        var command = new ImportTelemetryCommand(
            tenantId,
            farmId,
            missionId,
            request.OperationId,
            request.SourceFileName,
            request.SourceChecksum,
            request.ExpectedMissionVersion,
            points);

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
