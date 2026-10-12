using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;

public sealed record InitiateSurveyPaymentCommand(
    Guid SurveyOrderId,
    uint ExpectedOrderVersion)
    : IRequest<Result<InitiateSurveyPaymentResponse>>;
