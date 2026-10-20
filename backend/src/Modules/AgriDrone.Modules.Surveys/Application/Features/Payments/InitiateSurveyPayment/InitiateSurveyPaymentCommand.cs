using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;

public sealed record InitiateSurveyPaymentCommand(
    Guid SurveyOrderId,
    uint ExpectedOrderVersion,
    PaymentPurpose Purpose = PaymentPurpose.FinalBalance)
    : IRequest<Result<InitiateSurveyPaymentResponse>>;
