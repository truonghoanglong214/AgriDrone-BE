using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicCatalogueV3;

internal sealed class GetPublicCatalogueV3QueryHandler(
    ISurveyCatalogueQueries catalogueQueries,
    TimeProvider timeProvider)
    : IRequestHandler<GetPublicCatalogueV3Query, Result<PublicCatalogueV3Response?>>
{
    public async Task<Result<PublicCatalogueV3Response?>> Handle(
        GetPublicCatalogueV3Query request,
        CancellationToken cancellationToken)
    {
        var evaluatedAt = timeProvider.GetUtcNow();
        var result = await catalogueQueries.GetPublicCatalogueV3Async(
            evaluatedAt,
            cancellationToken);

        if (result is null)
        {
            return Result.Success<PublicCatalogueV3Response?>(null);
        }

        var response = new PublicCatalogueV3Response(
            EvaluatedAt: result.EvaluatedAt,
            IsBookable: result.IsBookable,
            PolicySummary: new PublicPolicySummaryResponse(
                result.PolicySummary.BundleVersionId,
                result.PolicySummary.BundleVersionNumber,
                result.PolicySummary.PolicyFingerprint,
                result.PolicySummary.PricingCurrency,
                result.PolicySummary.ClaimWindowDays,
                result.PolicySummary.ClaimTimeZoneId,
                result.PolicySummary.ExcludeNoticeDate,
                result.PolicySummary.RefundableBeforeCapture,
                result.PolicySummary.RetainDepositAfterCapture,
                result.PolicySummary.AllowedFreeNoShowReschedules),
            SharedDeposit: new PublicSharedDepositResponse(
                result.SharedDeposit.DepositPolicyId,
                result.SharedDeposit.VersionNumber,
                result.SharedDeposit.Amount,
                result.SharedDeposit.Currency),
            Services: result.Services.Select(s => new PublicServiceItemResponse(
                s.SurveyServiceId,
                s.Code,
                s.RevisionId,
                s.RevisionVersionNumber,
                s.Name,
                s.Description,
                s.MapsPoles,
                s.ScreensDisease,
                s.PriceVersionId,
                s.PriceVersionNumber,
                s.PricePerPole,
                s.Currency,
                s.PreliminaryLimitations)).ToArray());

        return Result.Success<PublicCatalogueV3Response?>(response);
    }
}
