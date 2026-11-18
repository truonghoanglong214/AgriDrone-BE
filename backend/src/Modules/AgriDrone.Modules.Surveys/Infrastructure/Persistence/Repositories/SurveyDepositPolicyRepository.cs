using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyDepositPolicyRepository(SurveysDbContext context)
    : ISurveyDepositPolicyRepository
{
    public Task<LegacySurveyDepositPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        context.LegacySurveyDepositPolicies
            .AsNoTracking()
            .Where(policy => policy.EffectiveTo == null)
            .OrderByDescending(policy => policy.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LegacySurveyDepositPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default) =>
        context.LegacySurveyDepositPolicies
            .FromSqlRaw("""
                SELECT * FROM survey.survey_deposit_policies
                WHERE effective_to IS NULL
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LegacySurveyDepositPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default) =>
        await context.LegacySurveyDepositPolicies.AsNoTracking()
            .OrderByDescending(policy => policy.VersionNumber)
            .ToArrayAsync(cancellationToken);

    public void Add(LegacySurveyDepositPolicy policy) =>
        context.LegacySurveyDepositPolicies.Add(policy);
}
