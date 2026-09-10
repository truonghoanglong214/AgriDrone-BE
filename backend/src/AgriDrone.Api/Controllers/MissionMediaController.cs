using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;
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
public sealed class MissionMediaController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    [HttpPost(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/media/upload-sessions")]
    public async Task<IResult> CreateUploadSession(
        Guid farmId,
        Guid missionId,
        [FromBody]
        CreateMissionMediaUploadSessionRequest request,
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

        var command = new CreateUploadSessionCommand(
            tenantId,
            farmId,
            missionId,
            request.OperationId,
            request.FileName,
            request.MimeType,
            request.MediaType,
            request.FileSizeBytes,
            request.Sha256Checksum,
            request.ExpectedMissionVersion);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new CreateMissionMediaUploadSessionResponse(
                    value.UploadSessionId,
                    value.MediaAssetId,
                    value.UploadUri.AbsoluteUri,
                    value.ExpiresAt,
                    value.Status.ToString(),
                    value.MissionVersion,
                    value.ReusedOperation)));
    }

    [HttpPost(
    "api/farms/{farmId:guid}/missions/" +
    "{missionId:guid}/media/upload-sessions/" +
    "{uploadSessionId:guid}/complete")]
    public async Task<IResult> CompleteUploadSession(
    Guid farmId,
    Guid missionId,
    Guid uploadSessionId,
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

        var command = new CompleteUploadSessionCommand(
            tenantId,
            farmId,
            missionId,
            uploadSessionId);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new CompleteMissionMediaUploadSessionResponse(
                    value.UploadSessionId,
                    value.MediaAssetId,
                    value.Status.ToString(),
                    value.MimeType,
                    value.FileSizeBytes,
                    value.Sha256Checksum,
                    value.ReusedCompletion)));
    }

}
