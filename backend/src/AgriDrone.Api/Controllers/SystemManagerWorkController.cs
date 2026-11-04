using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
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

    /// <remarks>Tìm Drone còn khả dụng trong khung giờ của lịch hẹn đã xác nhận cho SurveyOrder.</remarks>
    [HttpGet("survey-orders/{surveyOrderId:guid}/drones/available")]
    public async Task<IResult> GetAvailableDronesForOrder(
        Guid surveyOrderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAvailableDronesForOrderQuery(surveyOrderId),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Chuẩn bị mission theo SurveyOrder authoritative và lịch hẹn đã xác nhận. Giờ bắt đầu và kết thúc thực tế được ghi khi chuyển trạng thái chuyến bay. Gửi Idempotency-Key dạng UUID trong header và dùng lại đúng key đó khi retry.</remarks>
    [HttpPost("survey-orders/{surveyOrderId:guid}/missions/prepare")]
    public async Task<IResult> PrepareMissionSet(
        Guid surveyOrderId,
        [FromBody] PrepareMissionSetRequest request,
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
        var result = await sender.Send(
            new PrepareMissionSetCommand(
                surveyOrderId,
                request.DroneId,
                idempotencyKey),
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
