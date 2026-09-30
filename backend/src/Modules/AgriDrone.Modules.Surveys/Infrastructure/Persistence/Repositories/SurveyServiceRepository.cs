using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyServiceRepository(
    SurveysDbContext context) : ISurveyServiceRepository
{
    public Task<SurveyService?> GetByIdAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default) =>
        context.SurveyServices.SingleOrDefaultAsync(
            service => service.Id == serviceId,
            cancellationToken);

    public Task<SurveyService?> GetByIdWithPricesAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default) =>
        context.SurveyServices
            .Include(service => service.Prices)
            .SingleOrDefaultAsync(
                service => service.Id == serviceId,
                cancellationToken);
}
