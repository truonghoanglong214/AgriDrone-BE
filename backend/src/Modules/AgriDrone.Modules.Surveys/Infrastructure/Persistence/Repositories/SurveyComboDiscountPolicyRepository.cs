using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyComboDiscountPolicyRepository(SurveysDbContext context)
    : ISurveyComboDiscountPolicyRepository
{
    public Task<SurveyComboDiscountPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        context.SurveyComboDiscountPolicies
            .AsNoTracking()
            .Where(policy => policy.EffectiveTo == null)
            .OrderByDescending(policy => policy.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SurveyComboDiscountPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default) =>
        context.SurveyComboDiscountPolicies
            .FromSqlRaw("""
                SELECT * FROM survey.survey_combo_discount_policies
                WHERE effective_to IS NULL
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SurveyComboDiscountPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default) =>
        await context.SurveyComboDiscountPolicies.AsNoTracking()
            .OrderByDescending(policy => policy.VersionNumber)
            .ToArrayAsync(cancellationToken);

    public void Add(SurveyComboDiscountPolicy policy) =>
        context.SurveyComboDiscountPolicies.Add(policy);
}

