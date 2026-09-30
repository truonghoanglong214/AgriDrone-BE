using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class SurveyCatalogueQueries(
    SurveysDbContext context) : ISurveyCatalogueQueries
{
    public async Task<IReadOnlyList<SurveyServiceCatalogueRecord>>
        GetServicesAsync(CancellationToken cancellationToken = default) =>
        await context.SurveyServices
            .AsNoTracking()
            .OrderBy(service => service.Code)
            .Select(service => new SurveyServiceCatalogueRecord(
                service.Id,
                service.Code,
                service.Name,
                service.Description,
                service.ServiceType,
                service.Status,
                service.Version,
                service.CreatedAt,
                service.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SurveyServicePriceRecord>>
        GetPriceHistoryAsync(
            Guid serviceId,
            CancellationToken cancellationToken = default) =>
        await context.SurveyServicePrices
            .AsNoTracking()
            .Where(price => price.SurveyServiceId == serviceId)
            .OrderByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.CreatedAt)
            .Select(price => new SurveyServicePriceRecord(
                price.Id,
                price.SurveyServiceId,
                price.PricePerHa,
                price.Currency,
                price.EffectiveFrom,
                price.EffectiveTo,
                price.CreatedBy,
                price.CreatedAt))
            .ToListAsync(cancellationToken);
}
