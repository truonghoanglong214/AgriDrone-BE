using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class HarvestReadinessCriterionQueries(
    SurveysDbContext context) : IHarvestReadinessCriterionQueries
{
    public async Task<HarvestReadinessCriterionVersion?> GetEffectiveAsync(
        string code,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureUtc(evaluatedAt);
        var normalizedCode = code.Trim().ToUpperInvariant();
        var matches = await BaseQuery()
            .Where(criterion =>
                criterion.Code == normalizedCode &&
                criterion.Status != HarvestReadinessCriterionStatus.Retired &&
                criterion.EffectiveFrom <= evaluatedAt &&
                (!criterion.EffectiveTo.HasValue ||
                 evaluatedAt < criterion.EffectiveTo.Value))
            .OrderByDescending(criterion => criterion.VersionNumber)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Criterion '{normalizedCode}' has overlapping effective versions at '{evaluatedAt:O}'.");
        }

        return matches.SingleOrDefault();
    }

    public async Task<IReadOnlyList<HarvestReadinessCriterionVersion>>
        GetHistoryAsync(
            string code,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await BaseQuery()
            .Where(criterion => criterion.Code == normalizedCode)
            .OrderByDescending(criterion => criterion.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HarvestReadinessAssessmentCriterionSnapshot>>
        GetResultSnapshotsAsync(
            Guid surveyResultId,
            CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(surveyResultId, Guid.Empty);
        return await context.HarvestReadinessAssessments
            .AsNoTracking()
            .Where(assessment => assessment.SurveyResultId == surveyResultId)
            .OrderBy(assessment => assessment.CreatedAt)
            .Select(assessment =>
                new HarvestReadinessAssessmentCriterionSnapshot(
                    assessment.Id,
                    assessment.SurveyResultId,
                    assessment.HarvestReadinessCriterionId,
                    assessment.CriteriaCode ?? assessment.CriteriaVersion,
                    assessment.CriteriaVersionNumber,
                    assessment.CriteriaStatusSnapshot ?? "LEGACY",
                    assessment.AssessmentGranularity,
                    assessment.AiModelVersionId,
                    assessment.AiThresholdProfileId))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<HarvestReadinessCriterionVersion> BaseQuery() =>
        context.HarvestReadinessCriteria
            .AsNoTracking()
            .Select(criterion => new HarvestReadinessCriterionVersion(
                criterion.Id,
                criterion.Code,
                criterion.VersionNumber,
                criterion.Name,
                criterion.Description,
                criterion.Granularity,
                criterion.Status,
                criterion.EffectiveFrom,
                criterion.EffectiveTo));

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                nameof(value));
        }
    }
}
