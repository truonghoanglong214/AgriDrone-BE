namespace AgriDrone.Modules.Surveys.Domain;

internal interface IBusinessPolicyRepository
{
    // Revisions
    Task<SurveyServiceRevision?> GetLatestRevisionAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default);

    Task<SurveyServiceRevision?> GetEffectiveRevisionAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<SurveyServiceRevision?> GetRevisionByIdAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    void AddRevision(SurveyServiceRevision revision);

    // Deposit Policies
    Task<SurveyDepositPolicy?> GetLatestDepositPolicyAsync(
        CancellationToken cancellationToken = default);

    Task<SurveyDepositPolicy?> GetEffectiveDepositPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<SurveyDepositPolicy?> GetDepositPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddDepositPolicy(SurveyDepositPolicy policy);

    // Claim Policies
    Task<ClaimPolicy?> GetLatestClaimPolicyAsync(
        CancellationToken cancellationToken = default);

    Task<ClaimPolicy?> GetEffectiveClaimPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<ClaimPolicy?> GetClaimPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddClaimPolicy(ClaimPolicy policy);

    // Cancellation Policies
    Task<CancellationPolicy?> GetLatestCancellationPolicyAsync(
        CancellationToken cancellationToken = default);

    Task<CancellationPolicy?> GetEffectiveCancellationPolicyAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<CancellationPolicy?> GetCancellationPolicyByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddCancellationPolicy(CancellationPolicy policy);

    // Business Policy Versions (Bundles)
    Task<BusinessPolicyVersion?> GetLatestBundleVersionAsync(
        string streamKey,
        CancellationToken cancellationToken = default);

    Task<BusinessPolicyVersion?> GetEffectiveBundleVersionAsync(
        string streamKey,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<BusinessPolicyVersion?> GetBundleVersionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AddBundleVersion(BusinessPolicyVersion version);

    // Business Policy Activation
    Task<BusinessPolicyActivation?> GetActivationAsync(
        CancellationToken cancellationToken = default);

    void AddActivation(BusinessPolicyActivation activation);

    void AddActivationDecision(BusinessPolicyActivationDecision decision);

    // V3 Prices
    Task<SurveyServicePrice?> GetLatestPriceV3Async(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default);

    Task<SurveyServicePrice?> GetEffectivePriceV3Async(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    void AddPriceV3(SurveyServicePrice price);
}
