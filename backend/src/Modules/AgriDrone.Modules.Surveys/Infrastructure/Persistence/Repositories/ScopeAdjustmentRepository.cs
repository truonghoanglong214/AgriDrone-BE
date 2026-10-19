using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class ScopeAdjustmentRepository(SurveysDbContext context)
    : IScopeAdjustmentRepository
{
    public Task<ScopeAdjustment?> GetAsync(
        Guid surveyOrderId,
        Guid adjustmentId,
        CancellationToken cancellationToken = default) =>
        context.ScopeAdjustments.SingleOrDefaultAsync(
            item => item.SurveyOrderId == surveyOrderId && item.Id == adjustmentId,
            cancellationToken);

    public Task<ScopeAdjustment?> GetOpenAdjustmentAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default) =>
        context.ScopeAdjustments.SingleOrDefaultAsync(
            item => item.SurveyOrderId == surveyOrderId &&
                    (item.Status == ScopeAdjustmentStatus.Pending || item.Status == ScopeAdjustmentStatus.Approved),
            cancellationToken);

    public void Add(ScopeAdjustment adjustment) =>
        context.ScopeAdjustments.Add(adjustment);
}
