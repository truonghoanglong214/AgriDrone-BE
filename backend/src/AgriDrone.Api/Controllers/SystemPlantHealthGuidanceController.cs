using AgriDrone.Api.Contracts.PlantConditions;
using AgriDrone.Modules.Plants.Application.Features.Guidance.ActivateSourcedGuidance;
using AgriDrone.Modules.Plants.Application.Features.Guidance.CreateSourcedGuidance;
using AgriDrone.Modules.Plants.Application.Features.Guidance.GetSourcedGuidanceHistory;
using AgriDrone.Modules.Plants.Application.Features.Guidance.RetireSourcedGuidance;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/plant-health/guidance")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemPlantHealthGuidanceController(ISender sender)
    : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetHistory(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSourcedGuidanceHistoryQuery(), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost]
    public async Task<IResult> Create(
        [FromBody] CreateSourcedGuidanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSourcedGuidanceCommand(
            request.Code,
            request.PlantConditionId,
            request.SourceTitle,
            request.SourceReference,
            request.GuidanceText,
            request.EffectiveFrom,
            request.EffectiveTo), cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    [HttpPost("{guidanceId:guid}/activate")]
    public async Task<IResult> Activate(
        Guid guidanceId,
        [FromBody] ChangeSourcedGuidanceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ActivateSourcedGuidanceCommand(guidanceId, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost("{guidanceId:guid}/retire")]
    public async Task<IResult> Retire(
        Guid guidanceId,
        [FromBody] ChangeSourcedGuidanceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RetireSourcedGuidanceCommand(guidanceId, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, () => Results.NoContent());
    }
}
