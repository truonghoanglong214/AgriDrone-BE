using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Payments.GetSystemAdminSurveyPaymentHistory;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

internal sealed class GetSurveyPaymentHistoryQueryHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    IExecutionContext executionContext)
    : IRequestHandler<GetSurveyPaymentHistoryQuery, Result<SurveyPaymentHistoryResponse>>,
      IRequestHandler<GetSystemAdminSurveyPaymentHistoryQuery,
          Result<SurveyPaymentHistoryResponse>>
{
    public async Task<Result<SurveyPaymentHistoryResponse>> Handle(
        GetSurveyPaymentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SurveyPaymentHistoryResponse>(
                PaymentError.Forbidden());
        }

        var historyResult = await LoadOwnerHistoryAsync(
            query.SurveyOrderId,
            actorId,
            cancellationToken);
        return MapHistory(query.SurveyOrderId, historyResult);
    }

    public async Task<Result<SurveyPaymentHistoryResponse>> Handle(
        GetSystemAdminSurveyPaymentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SurveyPaymentHistoryResponse>(
                PaymentError.Forbidden());
        }

        var historyResult = await unitOfWork.ExecuteInTransactionAsync(
            token => repository.GetSystemAdminPaymentHistoryAsync(
                query.SurveyOrderId,
                token),
            cancellationToken);
        return MapHistory(query.SurveyOrderId, historyResult);
    }

    private Task<Result<IReadOnlyList<SurveyPayment>>> LoadOwnerHistoryAsync(
        Guid surveyOrderId,
        Guid actorId,
        CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(
        token => repository.GetOwnerPaymentHistoryAsync(
            surveyOrderId,
            actorId,
            token),
        cancellationToken);

    private static Result<SurveyPaymentHistoryResponse> MapHistory(
        Guid surveyOrderId,
        Result<IReadOnlyList<SurveyPayment>> historyResult)
    {
        if (historyResult.IsFailure)
        {
            return Result.Failure<SurveyPaymentHistoryResponse>(
                historyResult.Error);
        }

        var payments = historyResult.Value
            .Select(payment => new SurveyPaymentHistoryItemResponse(
                payment.Id,
                MapPurpose(payment.Purpose),
                payment.Amount,
                payment.Currency,
                payment.Provider,
                payment.ProviderReference,
                MapStatus(payment.Status),
                payment.CreatedAt,
                payment.UpdatedAt,
                payment.ConfirmedAt,
                payment.Version,
                payment.Events
                    .OrderBy(paymentEvent => paymentEvent.OccurredAt)
                    .Select(paymentEvent => new SurveyPaymentHistoryEventResponse(
                        paymentEvent.EventType,
                        paymentEvent.OccurredAt,
                        paymentEvent.ReceivedAt))
                    .ToArray()))
            .ToArray();

        return Result.Success(new SurveyPaymentHistoryResponse(
            surveyOrderId,
            payments));
    }

    private static string MapPurpose(PaymentPurpose purpose) => purpose switch
    {
        PaymentPurpose.BaselineDeposit => "BASELINE_DEPOSIT",
        PaymentPurpose.FinalBalance => "FINAL_BALANCE",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    private static string MapStatus(SurveyPaymentStatus status) => status switch
    {
        SurveyPaymentStatus.Pending => "PENDING",
        SurveyPaymentStatus.Processing => "PROCESSING",
        SurveyPaymentStatus.Confirmed => "CONFIRMED",
        SurveyPaymentStatus.Failed => "FAILED",
        SurveyPaymentStatus.Refunded => "REFUNDED",
        SurveyPaymentStatus.AdjustmentRequired => "ADJUSTMENT_REQUIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
