using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using FluentValidation;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

public sealed record ReconcileV3SurveyOrderFundingCommand(
    Guid OrderId,
    Guid OperationId,
    SurveyReconciliationDispositionKind DispositionKind,
    Guid? SurveyPaymentId,
    Guid? FundingReceiptId,
    Guid? TargetInvoiceRevisionId,
    decimal Amount,
    string Currency,
    string? ProviderReference,
    string? ProviderTransactionId,
    string Reason,
    string EvidenceReference,
    uint ExpectedOrderVersion,
    uint? ExpectedPaymentVersion = null)
    : IRequest<Result<V3FundingReconciliationResult>>;

internal sealed class ReconcileV3SurveyOrderFundingCommandValidator
    : AbstractValidator<ReconcileV3SurveyOrderFundingCommand>
{
    public ReconcileV3SurveyOrderFundingCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.OperationId).NotEmpty();
        RuleFor(c => c.DispositionKind).IsInEnum();
        RuleFor(c => c)
            .Must(c => c.SurveyPaymentId.HasValue || c.FundingReceiptId.HasValue)
            .WithMessage("Either SurveyPaymentId or FundingReceiptId is required.");
        RuleFor(c => c.SurveyPaymentId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("SurveyPaymentId cannot be empty when supplied.");
        RuleFor(c => c.FundingReceiptId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("FundingReceiptId cannot be empty when supplied.");
        RuleFor(c => c.TargetInvoiceRevisionId)
            .NotEmpty()
            .When(c => c.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice);
        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .When(c => c.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded);
        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0m)
            .When(c => c.DispositionKind == SurveyReconciliationDispositionKind.KeepExcluded);
        RuleFor(c => c.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");
        RuleFor(c => c.ProviderReference).MaximumLength(150);
        RuleFor(c => c.ProviderTransactionId).MaximumLength(150);
        RuleFor(c => c.Reason)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .MaximumLength(2000);
        RuleFor(c => c.EvidenceReference)
            .NotEmpty()
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .MaximumLength(1000);
        RuleFor(c => c.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(c => c.ExpectedPaymentVersion)
            .GreaterThan(0u)
            .When(c => c.ExpectedPaymentVersion.HasValue);
    }
}

internal sealed class ReconcileV3SurveyOrderFundingCommandHandler(
    IV3FundingReconciliationTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<ReconcileV3SurveyOrderFundingCommand, Result<V3FundingReconciliationResult>>
{
    public async Task<Result<V3FundingReconciliationResult>> Handle(
        ReconcileV3SurveyOrderFundingCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<V3FundingReconciliationResult>(PaymentError.Forbidden());
        }

        return await transaction.ReconcileAsync(
            new V3FundingReconciliationRequest(
                OrderId: command.OrderId,
                ActorId: actorId,
                CorrelationId: executionContext.CorrelationId,
                OperationId: command.OperationId,
                DispositionKind: command.DispositionKind,
                SurveyPaymentId: command.SurveyPaymentId,
                FundingReceiptId: command.FundingReceiptId,
                TargetInvoiceRevisionId: command.TargetInvoiceRevisionId,
                Amount: command.Amount,
                Currency: command.Currency,
                ProviderReference: command.ProviderReference,
                ProviderTransactionId: command.ProviderTransactionId,
                Reason: command.Reason,
                EvidenceReference: command.EvidenceReference,
                ExpectedOrderVersion: command.ExpectedOrderVersion,
                ExpectedPaymentVersion: command.ExpectedPaymentVersion),
            cancellationToken);
    }
}
