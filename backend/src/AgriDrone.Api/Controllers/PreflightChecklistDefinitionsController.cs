using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetAllPreflightChecklistDefinitions;
using AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/preflight-checklist")]
public sealed class PreflightChecklistDefinitionsController(ISender sender)
    : ControllerBase
{
    /// <summary>Lấy toàn bộ checklist preflight và các phiên bản của chúng.</summary>
    /// <remarks>SystemAdmin và SystemManager được xem; không cần farmId, missionId hay điều kiện lọc.</remarks>
    [HttpGet]
    [Authorize(Roles = SystemRoleCodes.SystemAdmin + "," + SystemRoleCodes.SystemManager)]
    public async Task<IResult> GetAllChecklists(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAllPreflightChecklistDefinitionsQuery(),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Tạo bộ checklist preflight dùng chung cho tất cả Mission. Chỉ tạo được khi chưa có checklist active.</remarks>
    [HttpPost]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
    public async Task<IResult> CreateSharedChecklist(
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
            new ManagePreflightChecklistDefinitionCommand(
                items, CreateOnly: true),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Thêm và/hoặc xóa điều kiện trên bộ checklist dùng chung. BE tạo version mới, giữ nguyên các version và checklist đã lưu trước đó.</remarks>
    [HttpPatch("conditions")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
    public async Task<IResult> ChangeConditions(
        [FromBody] ManagePreflightChecklistConditionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ItemsToAdd.ValueKind is not JsonValueKind.Array ||
            request.KeysToRemove is null ||
            request.ItemsToAdd.GetArrayLength() == 0 && request.KeysToRemove.Count == 0 ||
            request.KeysToRemove.Any(string.IsNullOrWhiteSpace) ||
            request.KeysToRemove.Distinct(StringComparer.Ordinal).Count() !=
                request.KeysToRemove.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["conditions"] = ["Provide valid conditions to add and/or unique condition keys to remove."]
            });
        }

        using var itemsToAdd = JsonDocument.Parse(
            request.ItemsToAdd.GetRawText());
        var result = await sender.Send(
            new ManagePreflightChecklistDefinitionCommand(
                null,
                itemsToAdd,
                request.KeysToRemove),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
