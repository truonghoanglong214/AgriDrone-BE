using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.RetireSurveyService;

public sealed record RetireSurveyServiceResponse(
    Guid SurveyServiceId,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset UpdatedAt);
