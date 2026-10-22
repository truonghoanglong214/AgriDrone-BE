using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyCaptureAuthorizationRepository(SurveysDbContext context)
    : ISurveyCaptureAuthorizationRepository
{
    public Task<SurveyCaptureAuthorization?> GetAsync(
        Guid surveyOrderId,
        SurveyCaptureAuthorizationType type,
        CancellationToken cancellationToken = default) =>
        context.SurveyCaptureAuthorizations.SingleOrDefaultAsync(
            item => item.SurveyOrderId == surveyOrderId && item.Type == type,
            cancellationToken);

    public async Task<IReadOnlyList<SurveyCaptureAuthorization>> ListByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default) =>
        await context.SurveyCaptureAuthorizations
            .Where(item => item.SurveyOrderId == surveyOrderId)
            .OrderBy(item => item.Type)
            .ToListAsync(cancellationToken);
}
