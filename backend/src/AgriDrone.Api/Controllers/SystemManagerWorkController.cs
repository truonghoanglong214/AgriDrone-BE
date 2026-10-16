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
    /// <remarks>Trả các Farm được phân công cho SystemManager hiện tại để chọn công việc hiện trường.</remarks>
    [HttpGet("farms")]
    public async Task<IResult> GetMyAssignedFarms(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetMyAssignedFarmsQuery(),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Chuẩn bị mission theo SurveyOrder authoritative. Farm cần baseline sẽ có BaselineMapping trước; mission dịch vụ trả phí chỉ được bổ sung khi đủ điều kiện bản đồ, số trụ, giá và payment. BaselineStartAt/EndAt và ServiceStartAt/EndAt là hai cặp lịch độc lập; khi chưa có lịch phù hợp mission có thể ở Draft. OperationId hỗ trợ gọi lại an toàn.</remarks>
    [HttpPost("survey-orders/{surveyOrderId:guid}/missions/prepare")]
    public async Task<IResult> PrepareMissionSet(
        Guid surveyOrderId,
        [FromBody] PrepareMissionSetRequest request,
        CancellationToken cancellationToken)
    {
        MissionScheduleWindow? serviceWindow = null;
        if (request.ServiceStartAt.HasValue || request.ServiceEndAt.HasValue)
        {
            if (!request.ServiceStartAt.HasValue || !request.ServiceEndAt.HasValue)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["serviceWindow"] =
                    ["ServiceStartAt and ServiceEndAt must be supplied together."]
                });
            }

            serviceWindow = new MissionScheduleWindow(
                request.ServiceStartAt.Value,
                request.ServiceEndAt.Value);
        }

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
                serviceWindow,
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
