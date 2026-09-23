using AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.Api.Legacy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public sealed class HarvestCatalogController(
    ISender sender) : ControllerBase
{
    /// <summary>Lấy các fruit quality grade đang được phép chọn khi tạo record mới.</summary>
    [HttpGet("harvest-quality-grades")]
    [LegacyEndpoint(
        "harvest-quality-grades.catalog",
        "Harvest records and post-harvest quality grading are outside scope; Harvest Readiness is a Survey Service.")]
    public async Task<IResult> GetHarvestQualityGrades(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetActiveHarvestQualityGradesQuery(),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            grades => Results.Ok(grades));
    }
}
