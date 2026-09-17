using AgriDrone.Api.Contracts.PlantConditions;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/plant-conditions")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemPlantConditionsController(
    ISender sender) : ControllerBase
{
    /// <summary>Tạo condition mới trong danh mục sức khỏe cây.</summary>
    /// <remarks>
    /// System Admin tạo revision đầu tiên của một bệnh hoặc dạng tổn thương.
    /// Code được chuẩn hóa thành chữ hoa và phải duy nhất trong toàn bộ lịch sử
    /// của danh mục.
    /// </remarks>
    [HttpPost]
    public async Task<IResult> Create(
        [FromBody] CreatePlantConditionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePlantConditionCommand(
            request.Code,
            request.Name,
            request.ScientificName,
            MapConditionType(request.ConditionType),
            request.Description);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                MapResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    private static PlantConditionApiResponse MapResponse(
        PlantConditionResponse response) =>
        new(
            response.Id,
            response.Code,
            response.Name,
            response.ScientificName,
            MapConditionType(response.ConditionType),
            response.Description,
            response.RevisionNumber,
            response.IsActive,
            response.CreatedAt,
            response.RetiredAt,
            response.Version);

    private static ConditionType MapConditionType(
        PlantConditionTypeValue conditionType) =>
        conditionType switch
        {
            PlantConditionTypeValue.Disease =>
                ConditionType.Disease,
            PlantConditionTypeValue.AbioticDamage =>
                ConditionType.AbioticDamage,
            PlantConditionTypeValue.MechanicalDamage =>
                ConditionType.MechanicalDamage,
            PlantConditionTypeValue.Other =>
                ConditionType.Other,
            _ => (ConditionType)(-1)
        };

    private static PlantConditionTypeValue MapConditionType(
        ConditionType conditionType) =>
        conditionType switch
        {
            ConditionType.Disease =>
                PlantConditionTypeValue.Disease,
            ConditionType.AbioticDamage =>
                PlantConditionTypeValue.AbioticDamage,
            ConditionType.MechanicalDamage =>
                PlantConditionTypeValue.MechanicalDamage,
            ConditionType.Other =>
                PlantConditionTypeValue.Other,
            _ => throw new ArgumentOutOfRangeException(
                nameof(conditionType),
                conditionType,
                "Unsupported plant condition type.")
        };
}
