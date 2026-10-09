using AgriDrone.Api.Contracts.Surveys.Catalogue;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.ActivateSurveyService;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServiceCatalogue;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.MarkSurveyServiceExperimental;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.RetireSurveyService;
using AgriDrone.Modules.Surveys.Application.Features.Catalogue.UpdateSurveyServiceMetadata;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-services")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyServicesController(ISender sender)
    : ControllerBase
{
    /// <summary>Lấy catalogue dịch vụ và toàn bộ lịch sử giá.</summary>
    [HttpGet]
    public async Task<IResult> GetCatalogue(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyServiceCatalogueQuery(),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            services => Results.Ok(services.Select(MapCatalogueResponse)));
    }

    /// <summary>Cập nhật metadata hiển thị mà không sửa snapshot lịch sử.</summary>
    [HttpPut("{serviceId:guid}")]
    public async Task<IResult> UpdateMetadata(
        [FromRoute] Guid serviceId,
        [FromBody] UpdateSurveyServiceMetadataRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSurveyServiceMetadataCommand(
                serviceId,
                request.Name,
                request.Description,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(new SurveyServiceMetadataMutationApiResponse(
                response.SurveyServiceId,
                response.Code,
                response.Name,
                response.Description,
                MapStatus(response.Status),
                response.Version,
                response.UpdatedAt)));
    }

    /// <summary>Kích hoạt service khi đã có giá VND theo trụ hiện hành.</summary>
    [HttpPost("{serviceId:guid}/activate")]
    public async Task<IResult> Activate(
        [FromRoute] Guid serviceId,
        [FromBody] ChangeSurveyServiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ActivateSurveyServiceCommand(
                serviceId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapMutationResponse(
                response.SurveyServiceId,
                response.Status,
                response.Version,
                response.UpdatedAt)));
    }

    /// <summary>Đưa capability đang active về trạng thái thử nghiệm.</summary>
    [HttpPost("{serviceId:guid}/experimental")]
    public async Task<IResult> MarkExperimental(
        [FromRoute] Guid serviceId,
        [FromBody] ChangeSurveyServiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MarkSurveyServiceExperimentalCommand(
                serviceId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapMutationResponse(
                response.SurveyServiceId,
                response.Status,
                response.Version,
                response.UpdatedAt)));
    }

    /// <summary>Retire service để chặn request mới nhưng giữ lịch sử.</summary>
    [HttpPost("{serviceId:guid}/retire")]
    public async Task<IResult> Retire(
        [FromRoute] Guid serviceId,
        [FromBody] ChangeSurveyServiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RetireSurveyServiceCommand(
                serviceId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapMutationResponse(
                response.SurveyServiceId,
                response.Status,
                response.Version,
                response.UpdatedAt)));
    }

    private static SystemSurveyServiceApiResponse MapCatalogueResponse(
        SurveyServiceCatalogueResponse service) =>
        new(
            service.SurveyServiceId,
            service.Code,
            service.Name,
            service.Description,
            MapServiceType(service.ServiceType),
            MapStatus(service.Status),
            service.Version,
            service.CreatedAt,
            service.UpdatedAt,
            service.Prices.Select(price =>
                    new SystemSurveyServicePriceApiResponse(
                        price.PriceVersionId,
                        price.PricePerPole,
                        price.LegacyPricePerHa,
                        price.Currency,
                        price.EffectiveFrom,
                        price.EffectiveTo,
                        price.CreatedBy,
                        price.CreatedAt,
                        price.IsLegacyPerHectarePrice))
                .ToArray());

    private static SurveyServiceMutationApiResponse MapMutationResponse(
        Guid serviceId,
        SurveyServiceStatus status,
        uint version,
        DateTimeOffset updatedAt) =>
        new(serviceId, MapStatus(status), version, updatedAt);

    private static string MapStatus(SurveyServiceStatus status) =>
        status switch
        {
            SurveyServiceStatus.Experimental => "EXPERIMENTAL",
            SurveyServiceStatus.Active => "ACTIVE",
            SurveyServiceStatus.Retired => "RETIRED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported survey service status.")
        };

    private static string MapServiceType(SurveyServiceType serviceType) =>
        serviceType switch
        {
            SurveyServiceType.PlantHealth => "PLANT_HEALTH",
            SurveyServiceType.HarvestReadiness => "HARVEST_READINESS",
            _ => throw new ArgumentOutOfRangeException(
                nameof(serviceType),
                serviceType,
                "Unsupported survey service type.")
        };
}
