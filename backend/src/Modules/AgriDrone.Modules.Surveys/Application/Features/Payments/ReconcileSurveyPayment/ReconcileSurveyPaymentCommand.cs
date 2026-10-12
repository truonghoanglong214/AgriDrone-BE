using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

public sealed record ReconcileSurveyPaymentCommand(
    Guid SurveyPaymentId,
    string ProviderReference,
    decimal SettledAmount,
    string Currency,
    string Reason,
    string EvidenceReference,
    uint ExpectedPaymentVersion,
    uint ExpectedOrderVersion)
    : IRequest<Result<ReconcileSurveyPaymentResponse>>;
