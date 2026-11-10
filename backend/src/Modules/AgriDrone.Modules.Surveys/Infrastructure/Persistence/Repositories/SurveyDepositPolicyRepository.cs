using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class SurveyDepositPolicyRepository(SurveysDbContext context)
    : ISurveyDepositPolicyRepository
{
    public Task<SurveyDepositPolicy?> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        context.SurveyDepositPolicies
            .AsNoTracking()
            .Where(policy => policy.EffectiveTo == null)
            .OrderByDescending(policy => policy.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SurveyDepositPolicy?> GetCurrentForUpdateAsync(
        CancellationToken cancellationToken = default) =>
        context.SurveyDepositPolicies
            .FromSqlRaw("""
                SELECT * FROM survey.survey_deposit_policies
                WHERE effective_to IS NULL
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SurveyDepositPolicy>> GetHistoryAsync(
        CancellationToken cancellationToken = default) =>
        await context.SurveyDepositPolicies.AsNoTracking()
            .OrderByDescending(policy => policy.VersionNumber)
            .ToArrayAsync(cancellationToken);

    public void Add(SurveyDepositPolicy policy) =>
        context.SurveyDepositPolicies.Add(policy);
}
