using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;

public sealed record PublishSurveyServiceRevisionCommand(
    Guid SurveyServiceId,
    string Name,
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    int? ExpectedLatestVersionNumber,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishSurveyServiceRevisionResponse>>;

public sealed record PublishSurveyServiceRevisionResponse(
    Guid RevisionId,
    Guid SurveyServiceId,
    int VersionNumber,
    Guid? PredecessorVersionId,
    string Name,
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);
