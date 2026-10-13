using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicSurveyServices;

public sealed record GetPublicSurveyServicesQuery
    : IRequest<Result<IReadOnlyList<PublicSurveyServiceResponse>>>;
