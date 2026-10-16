using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/preflight-checklist-definitions")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class PreflightChecklistDefinitionsController(ISender sender)
    : ControllerBase
{
    /// <remarks>SystemAdmin kích hoạt phiên bản mới của checklist DRONE_PRE_FLIGHT từ danh sách Items. Checklist đã hoàn tất theo phiên bản cũ sẽ bị coi là stale khi kiểm tra StartFlight.</remarks>
    [HttpPut("drone-pre-flight/active")]
    public async Task<IResult> ActivateNewVersion(
        [FromBody] ManagePreflightChecklistDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.ValueKind != JsonValueKind.Array ||
            request.Items.GetArrayLength() == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = ["Checklist items must be a non-empty JSON array."]
            });
        }

        using var items = JsonDocument.Parse(request.Items.GetRawText());
        var result = await sender.Send(
            new ManagePreflightChecklistDefinitionCommand(items),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
