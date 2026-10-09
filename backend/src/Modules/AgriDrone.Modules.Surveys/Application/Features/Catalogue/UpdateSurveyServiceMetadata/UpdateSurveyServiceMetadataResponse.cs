using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.UpdateSurveyServiceMetadata;

public sealed record UpdateSurveyServiceMetadataResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset UpdatedAt);
