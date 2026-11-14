using System.Security.Cryptography;
using System.Text;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class BusinessPolicyVersion : Entity
{
    private BusinessPolicyVersion() { }

    public string StreamKey { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public Guid SurveyDepositPolicyId { get; private set; }
    public Guid ClaimPolicyId { get; private set; }
    public Guid CancellationPolicyId { get; private set; }
    public Guid[] ServiceRevisionIds { get; private set; } = [];
    public string PricingCurrency { get; private set; } = null!;
    public string PolicyFingerprint { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static BusinessPolicyVersion Create(
        Guid id,
        string streamKey,
        int versionNumber,
        Guid? predecessorVersionId,
        Guid surveyDepositPolicyId,
        Guid claimPolicyId,
        Guid cancellationPolicyId,
        IEnumerable<Guid> serviceRevisionIds,
        CurrencyCode pricingCurrency,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyDepositPolicyId, nameof(surveyDepositPolicyId));
        DomainGuard.NotEmpty(claimPolicyId, nameof(claimPolicyId));
        DomainGuard.NotEmpty(cancellationPolicyId, nameof(cancellationPolicyId));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);

        ArgumentNullException.ThrowIfNull(serviceRevisionIds);
        var revisionList = serviceRevisionIds.ToList();
        if (revisionList.Count == 0)
        {
            throw new ArgumentException(
                "A policy bundle must include at least one service revision.",
                nameof(serviceRevisionIds));
        }

        if (revisionList.Any(r => r == Guid.Empty))
        {
            throw new ArgumentException(
                "Service revision identifier cannot be empty.",
                nameof(serviceRevisionIds));
        }

        if (revisionList.Distinct().Count() != revisionList.Count)
        {
            throw new ArgumentException(
                "Duplicate service revision identifiers are not allowed in a policy bundle.",
                nameof(serviceRevisionIds));
        }

        var normalizedStreamKey = PolicyValidationGuard.NormalizeRequired(streamKey, 100, nameof(streamKey));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(pricingCurrency, nameof(pricingCurrency));
        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        var fingerprint = ComputeFingerprint(
            normalizedStreamKey,
            versionNumber,
            surveyDepositPolicyId,
            claimPolicyId,
            cancellationPolicyId,
            revisionList,
            validatedCurrency);

        var policyVersion = new BusinessPolicyVersion
        {
            Id = id,
            StreamKey = normalizedStreamKey,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            SurveyDepositPolicyId = surveyDepositPolicyId,
            ClaimPolicyId = claimPolicyId,
            CancellationPolicyId = cancellationPolicyId,
            ServiceRevisionIds = [.. revisionList],
            PricingCurrency = validatedCurrency,
            PolicyFingerprint = fingerprint,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReasonCode = normalizedReasonCode,
            EvidenceReference = normalizedEvidence
        };

        return policyVersion;
    }

    public static string ComputeFingerprint(
        string streamKey,
        int versionNumber,
        Guid surveyDepositPolicyId,
        Guid claimPolicyId,
        Guid cancellationPolicyId,
        IEnumerable<Guid> serviceRevisionIds,
        string pricingCurrency)
    {
        var sortedRevisions = serviceRevisionIds.OrderBy(x => x).Select(x => x.ToString("D"));
        var payload = $"{streamKey}:{versionNumber}:{surveyDepositPolicyId:D}:{claimPolicyId:D}:{cancellationPolicyId:D}:{string.Join(",", sortedRevisions)}:{pricingCurrency}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    public bool ContainsServiceRevision(Guid serviceRevisionId) =>
        Array.IndexOf(ServiceRevisionIds, serviceRevisionId) >= 0;

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt, nameof(evaluatedAt));
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo, nameof(effectiveTo));
        if (EffectiveTo.HasValue)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.WindowAlreadyClosed,
                "A closed policy version effective window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.InvalidEffectiveWindow,
                "Policy version effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
