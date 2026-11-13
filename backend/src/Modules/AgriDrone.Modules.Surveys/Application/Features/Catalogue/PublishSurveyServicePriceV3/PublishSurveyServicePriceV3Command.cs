using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;

public sealed record PublishSurveyServicePriceV3Command(
    Guid SurveyServiceId,
    decimal AmountPerPole,
    string Currency,
    int? ExpectedLatestPriceVersion,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishSurveyServicePriceV3Response>>;

public sealed record PublishSurveyServicePriceV3Response(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    int VersionNumber,
    Guid? PredecessorVersionId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);
