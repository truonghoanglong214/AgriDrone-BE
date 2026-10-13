using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyServiceRepository(SurveysDbContext context)
    : ISurveyServiceRepository
{
    public Task<SurveyService?> GetByIdAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyServiceId,
            Guid.Empty);

        return context.SurveyServices
            .Include(service => service.Prices)
            .SingleOrDefaultAsync(
                service => service.Id == surveyServiceId,
                cancellationToken);
    }

    public Task<SurveyService?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalizedCode = code.Trim().ToUpperInvariant();

        return context.SurveyServices
            .Include(service => service.Prices)
            .SingleOrDefaultAsync(
                service => service.Code == normalizedCode,
                cancellationToken);
    }

    public void Add(SurveyService surveyService)
    {
        ArgumentNullException.ThrowIfNull(surveyService);
        context.SurveyServices.Add(surveyService);
    }
}
