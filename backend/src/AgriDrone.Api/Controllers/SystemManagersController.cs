using AgriDrone.Api.Contracts.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/managers")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemManagersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> Create(
        [FromBody] CreateSystemManagerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSystemManagerProfileCommand(request.UserId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    [HttpPut("{profileId:guid}/activate")]
    public async Task<IResult> Activate(
        [FromRoute] Guid profileId,
        [FromBody] SystemManagerStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ActivateSystemManagerProfileCommand(
                profileId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPut("{profileId:guid}/suspend")]
    public async Task<IResult> Suspend(
        [FromRoute] Guid profileId,
        [FromBody] SystemManagerStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SuspendSystemManagerProfileCommand(
                profileId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPut("{profileId:guid}/availability")]
    public async Task<IResult> UpdateAvailability(
        [FromRoute] Guid profileId,
        [FromBody] UpdateSystemManagerAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSystemManagerAvailabilityCommand(
                profileId,
                request.Availability,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPut("{profileId:guid}/qualification")]
    public async Task<IResult> UpdateQualification(
        [FromRoute] Guid profileId,
        [FromBody] UpdateSystemManagerQualificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSystemManagerQualificationCommand(
                profileId,
                request.Status,
                request.ExpiresAt,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
