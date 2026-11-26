using AgriDrone.Modules.Surveys.Domain;
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
    uint ExpectedOrderVersion,
    Guid? SurveyOrderId = null,
    Guid? OperationId = null,
    SurveyReconciliationDispositionKind? DispositionKind = null,
    Guid? FundingReceiptId = null,
    Guid? TargetInvoiceRevisionId = null,
    string? ProviderTransactionId = null)
    : IRequest<Result<ReconcileSurveyPaymentResponse>>;
