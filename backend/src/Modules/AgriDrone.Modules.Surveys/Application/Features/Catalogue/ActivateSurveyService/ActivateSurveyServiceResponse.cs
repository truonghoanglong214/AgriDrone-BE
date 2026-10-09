using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ActivateSurveyService;

public sealed record ActivateSurveyServiceResponse(
    Guid SurveyServiceId,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset UpdatedAt);
