using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/missions/{missionId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerMissionsController(ISender sender) : ControllerBase
{
    [HttpPost("field-notes")]
    public async Task<IResult> SyncFieldNote(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] SyncMissionFieldNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SyncFieldNoteCommand(
            farmId, missionId, request.OperationId, request.Text,
            request.ObservedAt), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpGet("field-notes")]
    public async Task<IResult> GetFieldNotes(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFieldNotesQuery(
            farmId, missionId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost("reschedule")]
    public async Task<IResult> Reschedule(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] RescheduleOrderMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RescheduleOrderMissionCommand(
            farmId, missionId, request.ExpectedVersion,
            request.StartAt, request.EndAt), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpGet("preflight/checklist")]
    public async Task<IResult> GetPreflightChecklist(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetActivePreflightChecklistQuery(farmId, missionId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ActivePreflightChecklistResponse(
                value.DefinitionId,
                value.Code,
                value.VersionNumber,
                value.Items)));
    }

    [HttpPost("preflight")]
    public async Task<IResult> CompletePreflight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] CompleteMissionPreflightRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Answers.ValueKind != JsonValueKind.Object)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answers"] = ["Checklist answers must be a JSON object."]
            });
        }

        using var answers = JsonDocument.Parse(request.Answers.GetRawText());
        var result = await sender.Send(
            new CompleteMissionPreflightCommand(
                farmId,
                missionId,
                request.OperationId,
                request.ExpectedVersion,
                request.ChecklistDefinitionId,
                request.ChecklistVersion,
                answers,
                request.SuitableForFlight,
                request.Notes,
                request.DeviceCompletedAt,
                request.UnsuitableConditionNotes,
                request.FailsafeNotes),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost("start")]
    public Task<IResult> Start(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.InFlight, request, cancellationToken);

    [HttpPost("complete-flight")]
    public Task<IResult> CompleteFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.FlightCompleted, request, cancellationToken);

    [HttpPost("fail-flight")]
    public Task<IResult> FailFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.FlightFailed, request, cancellationToken);

    [HttpPost("cancel")]
    public Task<IResult> Cancel(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.Cancelled, request, cancellationToken);

    private async Task<IResult> Transition(
        Guid farmId,
        Guid missionId,
        MissionStatus status,
        OperateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionMissionCommand(
                farmId,
                missionId,
                status,
                request.ExpectedVersion,
                request.Reason),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
