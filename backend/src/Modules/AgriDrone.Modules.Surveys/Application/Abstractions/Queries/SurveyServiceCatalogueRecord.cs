using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal sealed record SurveyServiceCatalogueRecord(
    Guid Id,
    string Code,
    string Name,
    string Description,
    SurveyServiceType ServiceType,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
