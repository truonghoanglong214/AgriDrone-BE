using AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicCatalogueV3;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/catalog")]
[AllowAnonymous]
public sealed class V3CatalogController(ISender sender) : ControllerBase
{
    /// <summary>Lấy danh mục dịch vụ khảo sát V3 công khai (MAP, COMBO).</summary>
    /// <remarks>
    /// Trả về các dịch vụ V3 đang hiệu lực kèm biểu giá theo trụ và chính sách cọc chung.
    /// Trường isBookable phản ánh cổng đặt hàng công khai (mặc định false đến khi go-live).
    /// </remarks>
    [HttpGet("survey-services")]
    [ProducesResponseType<PublicCatalogueV3Response>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetCatalogue(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicCatalogueV3Query(), cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            catalogue => catalogue is null
                ? Results.NotFound()
                : Results.Ok(catalogue));
    }
}
