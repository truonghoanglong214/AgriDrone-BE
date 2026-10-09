using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerWorkController(ISender sender) : ControllerBase
{
    [HttpGet("farms")]
    public async Task<IResult> GetMyAssignedFarms(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetMyAssignedFarmsQuery(),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost("survey-orders/{surveyOrderId:guid}/missions/prepare")]
    public async Task<IResult> PrepareMissionSet(
        Guid surveyOrderId,
        [FromBody] PrepareMissionSetRequest request,
        CancellationToken cancellationToken)
    {
        MissionScheduleWindow? baselineWindow = null;
        if (request.BaselineStartAt.HasValue ||
            request.BaselineEndAt.HasValue)
        {
            if (!request.BaselineStartAt.HasValue ||
                !request.BaselineEndAt.HasValue)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["baselineWindow"] =
                    ["BaselineStartAt and BaselineEndAt must be supplied together."]
                });
            }

            baselineWindow = new MissionScheduleWindow(
                request.BaselineStartAt.Value,
                request.BaselineEndAt.Value);
        }

        var result = await sender.Send(
            new PrepareMissionSetCommand(
                surveyOrderId,
                request.DroneId,
                request.OperationId,
                new MissionScheduleWindow(
                    request.ServiceStartAt,
                    request.ServiceEndAt),
                baselineWindow),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new PrepareMissionSetApiResponse(
                value.SurveyOrderId,
                value.FarmId,
                value.ReusedExistingSet,
                value.Missions.Select(mission =>
                    new PreparedMissionApiResponse(
                        mission.MissionId,
                        mission.Purpose.ToString(),
                        mission.Status.ToString(),
                        mission.ScheduledAt,
                        mission.ScheduledEndAt,
                        mission.Version)).ToArray())));
    }
}
