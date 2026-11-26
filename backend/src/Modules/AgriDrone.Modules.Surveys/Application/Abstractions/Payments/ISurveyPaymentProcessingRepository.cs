using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentProcessingRepository
{
    Task<Result<IReadOnlyList<SurveyPayment>>> GetOwnerPaymentHistoryAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SurveyPayment>>> GetSystemAdminPaymentHistoryAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyPaymentOwnerState>> GetForOwnerInitiationAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        PaymentPurpose purpose = PaymentPurpose.FinalBalance,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyPaymentEventState>> GetForProviderEventAsync(
        string provider,
        string providerReference,
        string deduplicationKey,
        string? providerTransactionId = null,
        (decimal Amount, string Currency)? receivedMoney = null,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyPriceAdjustmentRequestState>>
        GetForPriceAdjustmentRequestAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);

    Task<Result<SurveyPriceAdjustmentReviewState>>
        GetForPriceAdjustmentReviewAsync(
            Guid surveyOrderId,
            Guid adjustmentId,
            CancellationToken cancellationToken = default);

    Task<Result<SurveyPaymentReconciliationState>>
        GetForReconciliationAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default);

    void AddPayment(SurveyPayment payment);
    void AddEvent(PaymentEvent paymentEvent);
    void AddFundingReceipt(SurveyFundingReceipt receipt);
    void AddFundingAllocation(SurveyFundingAllocation allocation);
    void AddFundingReversal(SurveyFundingReversal reversal);
    void AddRefundObligation(SurveyRefundObligation obligation);
    void AddPriceAdjustment(PriceAdjustment adjustment);
}

public sealed record SurveyPaymentOwnerState(
    SurveyOrder Order,
    Guid PaidServiceAppointmentId,
    SurveyPayment? ActivePayment,
    SurveyDepositObligation? DepositObligation);

public sealed record SurveyPaymentEventState(
    SurveyOrder Order,
    SurveyPayment Payment,
    bool EventAlreadyProcessed,
    SurveyDepositObligation? DepositObligation,
    SurveyCancellationReceiptDisposition? CancellationDecision,
    SurveyDepositReversalSource? ReversalSource = null,
    bool ReceiptAlreadyRecorded = false,
    SurveyBalanceInvoiceContext? CurrentInvoice = null,
    decimal PriorReceiptsForPaymentAmount = 0m);

public sealed record SurveyBalanceInvoiceContext(
    Guid InvoiceRevisionId,
    string Currency,
    decimal TotalAmount,
    string Status);

public sealed record SurveyDepositReversalSource(
    Guid FundingReceiptId,
    decimal ReceivedAmount,
    decimal AllocatedAmount,
    decimal PriorCashReversed,
    decimal PriorAllocationReversed);

public sealed record SurveyCancellationReceiptDisposition(
    Guid DecisionId,
    CancellationDecisionKind DecisionKind);

public sealed record EffectiveSurveyPrice(
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency);

public sealed record SurveyPriceAdjustmentRequestState(
    SurveyOrder Order,
    EffectiveSurveyPrice EffectivePrice,
    PriceAdjustment? OpenAdjustment,
    SurveyPayment? ActivePayment);

public sealed record SurveyPriceAdjustmentReviewState(
    SurveyOrder Order,
    PriceAdjustment Adjustment,
    SurveyPayment? ActivePayment);

public sealed record SurveyPaymentReconciliationState(
    SurveyOrder Order,
    SurveyPayment Payment);
