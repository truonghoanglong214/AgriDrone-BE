using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServicePriceHistory;

public sealed record GetSurveyServicePriceHistoryQuery(Guid SurveyServiceId)
    : IRequest<Result<IReadOnlyList<SurveyServicePriceHistoryResponse>>>;
