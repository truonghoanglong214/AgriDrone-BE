using AgriDrone.Api.Contracts.Surveys.Catalogue;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicSurveyServices;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/public/survey-services")]
[AllowAnonymous]
public sealed class PublicSurveyServicesController(ISender sender)
    : ControllerBase
{
    /// <summary>Lấy các dịch vụ khảo sát hiện đang nhận yêu cầu.</summary>
    /// <remarks>
    /// Giá trả về là giá tham khảo hiện hành tính theo một trụ tại thời điểm
    /// server xử lý yêu cầu. Dịch vụ thử nghiệm được đánh dấu rõ bằng
    /// <c>isExperimental</c> và không được hiểu là năng lực đã được xác thực.
    /// Dịch vụ đã retire hoặc không có giá theo trụ đang hiệu lực sẽ bị loại.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PublicSurveyServiceApiResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IResult> GetCatalogue(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetPublicSurveyServicesQuery(),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            services => Results.Ok(services.Select(MapResponse)));
    }

    private static PublicSurveyServiceApiResponse MapResponse(
        PublicSurveyServiceResponse service) =>
        new(
            service.SurveyServiceId,
            service.Code,
            service.Name,
            service.Description,
            MapServiceType(service.ServiceType),
            service.IsExperimental,
            new PublicIndicativePriceApiResponse(
                service.IndicativePrice.AmountPerPole,
                service.IndicativePrice.Currency,
                service.IndicativePrice.EffectiveFrom,
                service.IndicativePrice.EffectiveTo));

    private static PublicSurveyServiceTypeValue MapServiceType(
        PublicSurveyServiceType serviceType) =>
        serviceType switch
        {
            PublicSurveyServiceType.PlantHealth =>
                PublicSurveyServiceTypeValue.PlantHealth,
            PublicSurveyServiceType.HarvestReadiness =>
                PublicSurveyServiceTypeValue.HarvestReadiness,
            _ => throw new ArgumentOutOfRangeException(
                nameof(serviceType),
                serviceType,
                "Unsupported public survey service type.")
        };
}
