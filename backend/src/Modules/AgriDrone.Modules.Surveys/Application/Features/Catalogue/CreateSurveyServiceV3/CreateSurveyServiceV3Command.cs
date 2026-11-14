using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;

public sealed record CreateSurveyServiceV3Command(
    string Code,
    string Name,
    string Description,
    bool ActivateImmediately = true) : IRequest<Result<CreateSurveyServiceV3Response>>;

public sealed record CreateSurveyServiceV3Response(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Scope,
    string Status,
    DateTimeOffset CreatedAt);
