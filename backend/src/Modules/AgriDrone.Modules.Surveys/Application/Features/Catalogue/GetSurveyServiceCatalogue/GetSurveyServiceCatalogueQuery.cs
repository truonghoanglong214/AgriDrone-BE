using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServiceCatalogue;

public sealed record GetSurveyServiceCatalogueQuery
    : IRequest<Result<IReadOnlyList<SurveyServiceCatalogueResponse>>>;
