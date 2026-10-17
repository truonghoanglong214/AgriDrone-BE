using AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.RevokeSurveyCaptureAuthorization;

public sealed record RevokeSurveyCaptureAuthorizationCommand(
    Guid SurveyOrderId,
    SurveyCaptureAuthorizationType Type,
    string Reason,
    uint ExpectedVersion) : IRequest<Result<SurveyCaptureAuthorizationResponse>>;
