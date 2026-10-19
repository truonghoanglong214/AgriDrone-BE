using AgriDrone.Api.Contracts.PlantConditions;
using AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;
using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class PlantCatalogController(
    ISender sender) : ControllerBase
{
    [HttpGet("plant-health")]
    public async Task<IResult> GetPlantHealth(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetPlantHealthMasterDataQuery(),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }

    /// <summary>Lấy các plant condition đang được phép chọn khi tạo record mới.</summary>
    [HttpGet("plant-conditions")]
    public async Task<IResult> GetPlantConditions(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetActivePlantConditionsQuery(),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            conditions => Results.Ok(conditions.Select(MapResponse)));
    }

    private static PlantConditionCatalogItemResponse MapResponse(
        PlantConditionCatalogResponse response) =>
        new(
            response.Id,
            response.Code,
            response.Name,
            response.ScientificName,
            MapConditionType(response.ConditionType),
            response.Description,
            response.RevisionNumber,
            response.PlantHealthLabelKind.HasValue
                ? MapLabelKind(response.PlantHealthLabelKind.Value)
                : null,
            response.DatasetVersion,
            response.AnnotationRuleVersion,
            response.TeamLabelLimitation);

    private static PlantConditionTypeValue MapConditionType(
        ConditionType conditionType) =>
        conditionType switch
        {
            ConditionType.Disease => PlantConditionTypeValue.Disease,
            ConditionType.AbioticDamage => PlantConditionTypeValue.AbioticDamage,
            ConditionType.MechanicalDamage => PlantConditionTypeValue.MechanicalDamage,
            ConditionType.Other => PlantConditionTypeValue.Other,
            _ => throw new ArgumentOutOfRangeException(
                nameof(conditionType),
                conditionType,
                "Unsupported plant condition type.")
        };

    private static PlantHealthLabelKindValue MapLabelKind(
        PlantHealthLabelKind value) => value switch
        {
            PlantHealthLabelKind.SuspectedDisease =>
                PlantHealthLabelKindValue.SuspectedDisease,
            PlantHealthLabelKind.ConfusingNonDisease =>
                PlantHealthLabelKindValue.ConfusingNonDisease,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
}
