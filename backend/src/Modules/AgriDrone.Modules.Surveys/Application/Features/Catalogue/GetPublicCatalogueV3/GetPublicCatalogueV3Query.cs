using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicCatalogueV3;

public sealed record GetPublicCatalogueV3Query : IRequest<Result<PublicCatalogueV3Response?>>;

public sealed record PublicCatalogueV3Response(
    DateTimeOffset EvaluatedAt,
    bool IsBookable,
    PublicPolicySummaryResponse PolicySummary,
    PublicSharedDepositResponse SharedDeposit,
    IReadOnlyList<PublicServiceItemResponse> Services);

public sealed record PublicPolicySummaryResponse(
    Guid BundleVersionId,
    int BundleVersionNumber,
    string PolicyFingerprint,
    string PricingCurrency,
    int ClaimWindowDays,
    string ClaimTimeZoneId,
    bool ExcludeNoticeDate,
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    int AllowedFreeNoShowReschedules);

public sealed record PublicSharedDepositResponse(
    Guid DepositPolicyId,
    int VersionNumber,
    decimal Amount,
    string Currency);

public sealed record PublicServiceItemResponse(
    Guid SurveyServiceId,
    string Code,
    Guid RevisionId,
    int RevisionVersionNumber,
    string Name,
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    Guid PriceVersionId,
    int PriceVersionNumber,
    decimal PricePerPole,
    string Currency,
    IReadOnlyList<string> PreliminaryLimitations);
