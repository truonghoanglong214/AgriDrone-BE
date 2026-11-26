using AgriDrone.Modules.Surveys.Application.Features.Policies.ReadCommercialPolicies;
using AgriDrone.Modules.Surveys.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence;

internal sealed class CommercialPolicyQueries(SurveysDbContext context)
    : IRequestHandler<GetCommercialServicesQuery, CommercialServiceItem[]>,
      IRequestHandler<GetCommercialRevisionsQuery, CommercialRevisionItem[]>,
      IRequestHandler<GetCommercialPricesQuery, CommercialPriceItem[]>,
      IRequestHandler<GetCommercialDepositPoliciesQuery, CommercialDepositPolicyItem[]>,
      IRequestHandler<GetCommercialClaimPoliciesQuery, CommercialClaimPolicyItem[]>,
      IRequestHandler<GetCommercialCancellationPoliciesQuery, CommercialCancellationPolicyItem[]>,
      IRequestHandler<GetCommercialBundleVersionsQuery, CommercialBundleVersionItem[]>,
      IRequestHandler<GetCommercialActivationQuery, CommercialActivationItem?>
{
    public Task<CommercialServiceItem[]> Handle(
        GetCommercialServicesQuery request,
        CancellationToken cancellationToken)
    {
        return context.SurveyServices
            .AsNoTracking()
            .Where(x => x.Scope == ContractScope.V3)
            .OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialServiceItem(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.Status))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialRevisionItem[]> Handle(
        GetCommercialRevisionsQuery request,
        CancellationToken cancellationToken)
    {
        return context.SurveyServiceRevisions
            .AsNoTracking()
            .Where(x => request.SurveyServiceId == null || x.SurveyServiceId == request.SurveyServiceId)
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialRevisionItem(
                x.Id,
                x.VersionNumber,
                x.PredecessorVersionId,
                x.SurveyServiceId,
                x.Name,
                x.Description,
                x.MapsPoles,
                x.ScreensDisease,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialPriceItem[]> Handle(
        GetCommercialPricesQuery request,
        CancellationToken cancellationToken)
    {
        return context.SurveyServicePrices
            .AsNoTracking()
            .Where(x => x.SurveyService.Scope == ContractScope.V3 && x.VersionNumber != null && x.PricePerPole != null)
            .Where(x => request.SurveyServiceId == null || x.SurveyServiceId == request.SurveyServiceId)
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialPriceItem(
                x.Id,
                x.VersionNumber!.Value,
                x.PredecessorVersionId,
                x.SurveyServiceId,
                x.PricePerPole!.Value.Amount,
                x.Currency,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialDepositPolicyItem[]> Handle(
        GetCommercialDepositPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        return context.SurveyDepositPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialDepositPolicyItem(
                x.Id,
                x.VersionNumber,
                x.PredecessorVersionId,
                x.Amount,
                x.Currency,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialClaimPolicyItem[]> Handle(
        GetCommercialClaimPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        return context.ClaimPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialClaimPolicyItem(
                x.Id,
                x.VersionNumber,
                x.PredecessorVersionId,
                x.CalendarWindowDays,
                x.TimeZoneId,
                x.ExcludeNoticeDate,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialCancellationPolicyItem[]> Handle(
        GetCommercialCancellationPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        return context.CancellationPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialCancellationPolicyItem(
                x.Id,
                x.VersionNumber,
                x.PredecessorVersionId,
                x.RefundableBeforeCapture,
                x.RetainDepositAfterCapture,
                x.AllowedFreeNoShowReschedules,
                x.OperationalPostponementRefundable,
                x.ServiceFailureRefundable,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialBundleVersionItem[]> Handle(
        GetCommercialBundleVersionsQuery request,
        CancellationToken cancellationToken)
    {
        return context.BusinessPolicyVersions
            .AsNoTracking()
            .Where(x => request.StreamKey == null || x.StreamKey == request.StreamKey)
            .OrderByDescending(x => x.VersionNumber).ThenBy(x => x.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(x => new CommercialBundleVersionItem(
                x.Id,
                x.VersionNumber,
                x.PredecessorVersionId,
                x.StreamKey,
                x.SurveyDepositPolicyId,
                x.ClaimPolicyId,
                x.CancellationPolicyId,
                x.ServiceRevisionIds,
                x.PricingCurrency,
                x.PolicyFingerprint,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.ReasonCode,
                x.EvidenceReference))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CommercialActivationItem?> Handle(
        GetCommercialActivationQuery request,
        CancellationToken cancellationToken)
    {
        return context.BusinessPolicyActivations
            .AsNoTracking()
            .Where(x => x.Id == BusinessPolicyActivation.SingletonId)
            .Select(x => new CommercialActivationItem(
                x.ActiveBusinessPolicyVersionId,
                x.PublicIntakeEnabled,
                x.CaptureEnabled,
                x.ReleaseEnabled,
                x.LastDecisionId,
                x.ActivatedAt,
                x.ReasonCode,
                x.EvidenceReference))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
