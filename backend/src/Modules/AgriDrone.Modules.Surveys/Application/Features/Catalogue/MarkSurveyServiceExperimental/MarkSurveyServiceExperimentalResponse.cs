using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.MarkSurveyServiceExperimental;

public sealed record MarkSurveyServiceExperimentalResponse(
    Guid SurveyServiceId,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset UpdatedAt);
