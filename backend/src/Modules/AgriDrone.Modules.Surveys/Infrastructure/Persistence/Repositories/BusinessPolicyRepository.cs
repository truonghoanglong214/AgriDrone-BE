using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;

internal sealed class BusinessPolicyRepository(SurveysDbContext context)
    : IBusinessPolicyRepository
{
    public Task<SurveyServiceRevision?> GetLatestRevisionAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyServiceRevisions
            .Where(r => r.SurveyServiceId == surveyServiceId)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SurveyServiceRevision?> GetEffectiveRevisionAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyServiceRevisions
            .Where(r => r.SurveyServiceId == surveyServiceId &&
                        r.EffectiveFrom <= evaluatedAt &&
                        (!r.EffectiveTo.HasValue || evaluatedAt < r.EffectiveTo.Value))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SurveyServiceRevision?> GetRevisionByIdAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyServiceRevisions
            .SingleOrDefaultAsync(r => r.Id == revisionId, cancellationToken);
    }

    public void AddRevision(SurveyServiceRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        context.SurveyServiceRevisions.Add(revision);
    }

    public Task<SurveyDepositPolicy?> GetLatestDepositPolicyAsync(
        CancellationToken cancellationToken = default)
    {
        return context.SurveyDepositPolicies
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SurveyDepositPolicy?> GetEffectiveDepositPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyDepositPolicies
            .Where(p => p.EffectiveFrom <= evaluatedAt &&
                        (!p.EffectiveTo.HasValue || evaluatedAt < p.EffectiveTo.Value))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SurveyDepositPolicy?> GetDepositPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyDepositPolicies
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void AddDepositPolicy(SurveyDepositPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        context.SurveyDepositPolicies.Add(policy);
    }

    public Task<ClaimPolicy?> GetLatestClaimPolicyAsync(
        CancellationToken cancellationToken = default)
    {
        return context.ClaimPolicies
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ClaimPolicy?> GetEffectiveClaimPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        return context.ClaimPolicies
            .Where(p => p.EffectiveFrom <= evaluatedAt &&
                        (!p.EffectiveTo.HasValue || evaluatedAt < p.EffectiveTo.Value))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ClaimPolicy?> GetClaimPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.ClaimPolicies
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void AddClaimPolicy(ClaimPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        context.ClaimPolicies.Add(policy);
    }

    public Task<CancellationPolicy?> GetLatestCancellationPolicyAsync(
        CancellationToken cancellationToken = default)
    {
        return context.CancellationPolicies
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CancellationPolicy?> GetEffectiveCancellationPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        return context.CancellationPolicies
            .Where(p => p.EffectiveFrom <= evaluatedAt &&
                        (!p.EffectiveTo.HasValue || evaluatedAt < p.EffectiveTo.Value))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CancellationPolicy?> GetCancellationPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.CancellationPolicies
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void AddCancellationPolicy(CancellationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        context.CancellationPolicies.Add(policy);
    }

    public Task<BusinessPolicyVersion?> GetLatestBundleVersionAsync(
        string streamKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamKey);

        return context.BusinessPolicyVersions
            .Where(v => v.StreamKey == streamKey.Trim())
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<BusinessPolicyVersion?> GetEffectiveBundleVersionAsync(
        string streamKey,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamKey);

        return context.BusinessPolicyVersions
            .Where(v => v.StreamKey == streamKey.Trim() &&
                        v.EffectiveFrom <= evaluatedAt &&
                        (!v.EffectiveTo.HasValue || evaluatedAt < v.EffectiveTo.Value))
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<BusinessPolicyVersion?> GetBundleVersionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.BusinessPolicyVersions
            .SingleOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public void AddBundleVersion(BusinessPolicyVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        context.BusinessPolicyVersions.Add(version);
    }

    public Task<BusinessPolicyActivation?> GetActivationAsync(
        CancellationToken cancellationToken = default)
    {
        return context.BusinessPolicyActivations
            .SingleOrDefaultAsync(a => a.Id == BusinessPolicyActivation.SingletonId, cancellationToken);
    }

    public void AddActivation(BusinessPolicyActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        context.BusinessPolicyActivations.Add(activation);
    }

    public void AddActivationDecision(BusinessPolicyActivationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        context.BusinessPolicyActivationDecisions.Add(decision);
    }

    public Task<SurveyServicePrice?> GetLatestPriceV3Async(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyServicePrices
            .Where(p => p.SurveyServiceId == surveyServiceId && p.VersionNumber.HasValue)
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SurveyServicePrice?> GetEffectivePriceV3Async(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        return context.SurveyServicePrices
            .Where(p => p.SurveyServiceId == surveyServiceId &&
                        p.VersionNumber.HasValue &&
                        p.PricePerPole.HasValue &&
                        p.EffectiveFrom <= evaluatedAt &&
                        (!p.EffectiveTo.HasValue || evaluatedAt < p.EffectiveTo.Value))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void AddPriceV3(SurveyServicePrice price)
    {
        ArgumentNullException.ThrowIfNull(price);
        context.SurveyServicePrices.Add(price);
    }
}
