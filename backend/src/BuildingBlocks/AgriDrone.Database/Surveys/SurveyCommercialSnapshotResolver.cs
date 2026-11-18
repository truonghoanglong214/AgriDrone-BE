using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

public sealed class SurveyCommercialSnapshotResolver(
    SurveyApprovalDbContext context)
    : ISurveyCommercialSnapshotResolver
{
    public async Task<Result<ResolvedSurveyCommercialSnapshot>> ResolveForApprovalAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        context.EnsureApprovalTransaction();

        // 1. Acquire FOR SHARE lock on active policy activation
        var activationLocked = await SurveyApprovalRowLock.ExistsAsync(
            context,
            """
            SELECT id
            FROM survey.business_policy_activation
            WHERE id = @activation_id
            FOR SHARE
            """,
            cancellationToken,
            SurveyApprovalRowLock.Id("activation_id", BusinessPolicyActivation.SingletonId));

        if (!activationLocked)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Active policy configuration pointer does not exist."));
        }

        // 2. Query BusinessPolicyActivation
        var activation = await context.BusinessPolicyActivations
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == BusinessPolicyActivation.SingletonId, cancellationToken);

        if (activation is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Active business policy activation record is missing."));
        }

        if (!activation.CaptureEnabled || !activation.ReleaseEnabled)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.ServiceUnavailable());
        }

        // 3. Query BusinessPolicyVersion
        var bundle = await context.BusinessPolicyVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == activation.ActiveBusinessPolicyVersionId, cancellationToken);

        if (bundle is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Active business policy version bundle is missing."));
        }

        if (bundle.EffectiveFrom > evaluatedAt || (bundle.EffectiveTo.HasValue && bundle.EffectiveTo.Value <= evaluatedAt))
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Active business policy version is not effective at the evaluation time."));
        }

        // 4. Query DepositPolicy
        var deposit = await context.SurveyDepositPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == bundle.SurveyDepositPolicyId, cancellationToken);

        if (deposit is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Survey deposit policy is missing."));
        }

        if (deposit.EffectiveFrom > evaluatedAt || (deposit.EffectiveTo.HasValue && deposit.EffectiveTo.Value <= evaluatedAt))
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Survey deposit policy is not effective at the evaluation time."));
        }

        // 5. Query ClaimPolicy
        var claim = await context.ClaimPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == bundle.ClaimPolicyId, cancellationToken);

        if (claim is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Claim policy is missing."));
        }

        if (claim.EffectiveFrom > evaluatedAt || (claim.EffectiveTo.HasValue && claim.EffectiveTo.Value <= evaluatedAt))
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Claim policy is not effective at the evaluation time."));
        }

        // 6. Query CancellationPolicy
        var cancellation = await context.CancellationPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == bundle.CancellationPolicyId, cancellationToken);

        if (cancellation is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Cancellation policy is missing."));
        }

        if (cancellation.EffectiveFrom > evaluatedAt || (cancellation.EffectiveTo.HasValue && cancellation.EffectiveTo.Value <= evaluatedAt))
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Cancellation policy is not effective at the evaluation time."));
        }

        // 7. Query SurveyService & Revision
        var service = await context.SurveyServices
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == surveyServiceId, cancellationToken);

        if (service is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.ServiceUnavailable());
        }

        var revision = await context.SurveyServiceRevisions
            .AsNoTracking()
            .Where(r => r.SurveyServiceId == surveyServiceId && bundle.ServiceRevisionIds.Contains(r.Id))
            .SingleOrDefaultAsync(cancellationToken);

        if (revision is null)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Service revision for the selected service is not pinned in the active bundle."));
        }

        if (revision.EffectiveFrom > evaluatedAt || (revision.EffectiveTo.HasValue && revision.EffectiveTo.Value <= evaluatedAt))
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Service revision is not effective at the evaluation time."));
        }

        // 8. Query effective price for the service
        var price = await context.SurveyServicePrices
            .AsNoTracking()
            .Where(p => p.SurveyServiceId == surveyServiceId &&
                        p.EffectiveFrom <= evaluatedAt &&
                        (p.EffectiveTo == null || p.EffectiveTo > evaluatedAt))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (price is null || !price.PricePerPole.HasValue || price.PricePerPole.Value.Amount <= 0)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationMissing("Active per-pole price for the service is missing."));
        }

        // Currency checks
        if (price.Currency != bundle.PricingCurrency || deposit.Currency != bundle.PricingCurrency)
        {
            return Result.Failure<ResolvedSurveyCommercialSnapshot>(
                SurveyApprovalError.PolicyConfigurationChanged("Mismatched currency between service price/deposit and policy bundle."));
        }

        return Result.Success(new ResolvedSurveyCommercialSnapshot(
            bundle.Id,
            bundle.PolicyFingerprint,
            revision.Id,
            revision.VersionNumber,
            service.Code,
            revision.MapsPoles,
            revision.ScreensDisease,
            price.Id,
            price.VersionNumber ?? 1,
            price.PricePerPole.Value.Amount,
            bundle.PricingCurrency,
            deposit.Id,
            deposit.VersionNumber,
            deposit.Amount,
            claim.Id,
            claim.VersionNumber,
            claim.CalendarWindowDays,
            claim.TimeZoneId,
            claim.ExcludeNoticeDate,
            cancellation.Id,
            cancellation.VersionNumber,
            cancellation.RefundableBeforeCapture,
            cancellation.RetainDepositAfterCapture,
            cancellation.AllowedFreeNoShowReschedules,
            evaluatedAt));
    }
}
