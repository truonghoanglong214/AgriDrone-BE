using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class V3OperationalContractsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid SurveyRequestId = Guid.NewGuid();
    private static readonly Guid SurveyServiceId = Guid.NewGuid();
    private static readonly Guid PolicyVersionId = Guid.NewGuid();
    private static readonly Guid PriceVersionId = Guid.NewGuid();
    private static readonly Guid DepositPolicyId = Guid.NewGuid();
    private static readonly Guid CancellationPolicyId = Guid.NewGuid();

    private static ResolvedSurveyCommercialSnapshot CreateSampleSnapshot(decimal depositAmount = 5_000m)
    {
        return new ResolvedSurveyCommercialSnapshot(
            BusinessPolicyVersionId: PolicyVersionId,
            PolicyFingerprint: "fingerprint-123",
            SurveyServiceRevisionId: Guid.NewGuid(),
            SurveyServiceRevisionVersion: 1,
            ServiceCode: "MAP",
            MapsPoles: true,
            ScreensDisease: false,
            PriceVersionId: PriceVersionId,
            PriceVersionNumber: 1,
            PricePerPole: 10_000m,
            Currency: "VND",
            DepositPolicyId: DepositPolicyId,
            DepositVersionNumber: 1,
            DepositAmount: depositAmount,
            ClaimPolicyId: Guid.NewGuid(),
            ClaimPolicyVersionNumber: 1,
            ClaimWindowDays: 7,
            ClaimTimeZoneId: "Asia/Ho_Chi_Minh",
            ExcludeNoticeDate: false,
            CancellationPolicyId: CancellationPolicyId,
            CancellationPolicyVersionNumber: 1,
            RefundableBeforeCapture: true,
            RetainDepositAfterCapture: true,
            AllowedFreeNoShowReschedules: 1,
            EvaluatedAt: Now);
    }

    [Theory]
    [InlineData("MAP", false)]
    [InlineData("COMBO", true)]
    public void V3ServicesCannotUseLegacyPostFlightOrCompletionShortcuts(string code, bool screensDisease)
    {
        var snapshot = CreateSampleSnapshot() with { ServiceCode = code, ScreensDisease = screensDisease };
        var order = SurveyOrder.CreateV3CaptureOrder("V3-GUARD", TenantId, FarmId,
            SurveyRequestId, SurveyServiceId, snapshot, requiresBaselineMapping: true, createdAt: Now);

        Assert.Throws<InvalidOperationException>(() => order.EnablePostFlightSettlement(
            SurveyServiceType.PlantHealth, PriceVersionId, snapshot.PricePerPole));
        Assert.Throws<SurveyDomainException>(() => order.MarkPaymentConfirmed(Now));
        Assert.Throws<SurveyDomainException>(() => order.Complete(Now));
        Assert.False(order.PostFlightSettlement);
        Assert.Equal(SurveyOrderStatus.AwaitingAppointment, order.Status);
        Assert.Equal(snapshot.DepositAmount, order.DepositAmount);
    }

    [Fact]
    public void V3OrderStatusTransitionsAreStrictlyEnforced()
    {
        // Valid forward flows
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingAppointment, SurveyOrderStatus.AwaitingDeposit, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingAppointment, SurveyOrderStatus.ReadyForCapture, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingAppointment, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingDeposit, SurveyOrderStatus.ReadyForCapture, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingDeposit, SurveyOrderStatus.AwaitingAppointment, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingDeposit, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.ReadyForCapture, SurveyOrderStatus.Capturing, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.ReadyForCapture, SurveyOrderStatus.AwaitingAppointment, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.ReadyForCapture, SurveyOrderStatus.AwaitingDeposit, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.ReadyForCapture, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.Capturing, SurveyOrderStatus.AwaitingCaptureUpload, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.Capturing, SurveyOrderStatus.AwaitingReflightDecision, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingCaptureUpload, SurveyOrderStatus.AwaitingInputReview, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingCaptureUpload, SurveyOrderStatus.AwaitingReflightDecision, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingCaptureUpload, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingInputReview, SurveyOrderStatus.AwaitingProcessing, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingInputReview, SurveyOrderStatus.AwaitingReflightDecision, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingInputReview, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingProcessing, SurveyOrderStatus.Processing, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingProcessing, SurveyOrderStatus.AwaitingReflightDecision, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingProcessing, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.Processing, SurveyOrderStatus.AwaitingSettlement, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.Processing, SurveyOrderStatus.AwaitingReflightDecision, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.Processing, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingReflightDecision, SurveyOrderStatus.AwaitingAppointment, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingReflightDecision, SurveyOrderStatus.AwaitingDeposit, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingReflightDecision, SurveyOrderStatus.ReadyForCapture, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingReflightDecision, SurveyOrderStatus.Cancelled, ContractScope.V3));

        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingSettlement, SurveyOrderStatus.Completed, ContractScope.V3));
        Assert.True(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingSettlement, SurveyOrderStatus.Cancelled, ContractScope.V3));

        // Invalid direct transitions
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.Capturing, SurveyOrderStatus.Completed, ContractScope.V3));
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.Capturing, SurveyOrderStatus.Cancelled, ContractScope.V3));
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.Completed, SurveyOrderStatus.AwaitingAppointment, ContractScope.V3));
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.Cancelled, SurveyOrderStatus.AwaitingAppointment, ContractScope.V3));

        // Scope isolation: Legacy status cannot transition to V3 status under V3 scope
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.AwaitingBaselineAppointment, SurveyOrderStatus.Capturing, ContractScope.V3));
        Assert.False(SurveyOrder.CanTransition(SurveyOrderStatus.Capturing, SurveyOrderStatus.PendingReview, ContractScope.Legacy));
    }

    [Fact]
    public void V3OrderLifecycleMethodsExecuteCorrectlyUnderV3Scope()
    {
        var snapshot = CreateSampleSnapshot();
        var order = SurveyOrder.CreateV3CaptureOrder(
            "ORD-V3-001",
            TenantId,
            FarmId,
            SurveyRequestId,
            SurveyServiceId,
            snapshot,
            requiresBaselineMapping: true,
            createdAt: Now);

        Assert.Equal(ContractScope.V3, order.Scope);
        Assert.Equal(SurveyOrderStatus.AwaitingAppointment, order.Status);
        Assert.Equal(0u, order.Version);

        // Bind Deposit Obligation
        var obligationId = Guid.NewGuid();
        order.BindDepositObligation(obligationId);
        Assert.Equal(obligationId, order.DepositObligationId);

        // Rebinding same obligation succeeds
        order.BindDepositObligation(obligationId);

        // Rebinding different obligation fails
        Assert.Throws<InvalidOperationException>(() => order.BindDepositObligation(Guid.NewGuid()));

        // Step by step transitions
        order.TransitionToAwaitingDepositV3(Now.AddMinutes(5));
        Assert.Equal(SurveyOrderStatus.AwaitingDeposit, order.Status);
        Assert.Equal(1u, order.Version);

        order.TransitionToReadyForCaptureV3(Now.AddMinutes(10));
        Assert.Equal(SurveyOrderStatus.ReadyForCapture, order.Status);
        Assert.Equal(2u, order.Version);

        order.TransitionToCapturingV3(Now.AddMinutes(15));
        Assert.Equal(SurveyOrderStatus.Capturing, order.Status);
        Assert.Equal(3u, order.Version);

        order.TransitionToAwaitingCaptureUploadV3(Now.AddMinutes(45));
        Assert.Equal(SurveyOrderStatus.AwaitingCaptureUpload, order.Status);
        Assert.Equal(4u, order.Version);

        order.TransitionToAwaitingInputReviewV3(Now.AddMinutes(50));
        Assert.Equal(SurveyOrderStatus.AwaitingInputReview, order.Status);
        Assert.Equal(5u, order.Version);

        order.TransitionToAwaitingProcessingV3(Now.AddMinutes(55));
        Assert.Equal(SurveyOrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(6u, order.Version);

        order.TransitionToProcessingV3(Now.AddMinutes(60));
        Assert.Equal(SurveyOrderStatus.Processing, order.Status);
        Assert.Equal(7u, order.Version);

        order.TransitionToAwaitingSettlementV3(Now.AddMinutes(120));
        Assert.Equal(SurveyOrderStatus.AwaitingSettlement, order.Status);
        Assert.Equal(8u, order.Version);

        order.TransitionToCompletedV3(Now.AddMinutes(150));
        Assert.Equal(SurveyOrderStatus.Completed, order.Status);
        Assert.Equal(9u, order.Version);
    }

    [Fact]
    public void V3TransitionMethodsRejectNonV3Orders()
    {
        var legacyOrder = SurveyOrder.Create(
            "LEGACY-001",
            TenantId,
            FarmId,
            SurveyRequestId,
            SurveyServiceId,
            requiresBaselineMapping: true,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "1.0",
            previousOrderCompatibilityReason: "Initial",
            createdAt: Now);

        Assert.Equal(ContractScope.Legacy, legacyOrder.Scope);
        var ex = Assert.Throws<SurveyDomainException>(() =>
            legacyOrder.TransitionToReadyForCaptureV3(Now.AddMinutes(5)));
        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void OrderStateHistoryEnforcesTransitionSequenceRules()
    {
        var orderId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        // Valid initial history with sequence 1
        var initial = OrderStateHistory.CreateInitial(
            Guid.NewGuid(),
            orderId,
            1,
            SurveyOrderStatus.AwaitingAppointment,
            actorId: null,
            source: "API",
            reason: "Order created",
            correlationId: correlationId,
            createdAt: Now,
            transitionSequence: 1);

        Assert.Equal(1u, initial.TransitionSequence);
        Assert.Null(initial.FromStatus);
        Assert.Equal(SurveyOrderStatus.AwaitingAppointment, initial.ToStatus);

        // Initial with sequence != 1 fails
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderStateHistory.CreateInitial(
                Guid.NewGuid(),
                orderId,
                1,
                SurveyOrderStatus.AwaitingAppointment,
                actorId: null,
                source: "API",
                reason: "Order created",
                correlationId: correlationId,
                createdAt: Now,
                transitionSequence: 2));

        // Valid transition with sequence >= 2
        var transition = OrderStateHistory.CreateTransition(
            Guid.NewGuid(),
            orderId,
            2,
            SurveyOrderStatus.AwaitingAppointment,
            SurveyOrderStatus.AwaitingDeposit,
            actorId: Guid.NewGuid(),
            source: "System",
            reason: "Appointment confirmed",
            evidenceReference: "ref-1",
            correlationId: correlationId,
            predecessorHistoryId: initial.Id,
            createdAt: Now.AddMinutes(5),
            transitionSequence: 2);

        Assert.Equal(2u, transition.TransitionSequence);
        Assert.Equal(initial.Id, transition.PredecessorHistoryId);

        // Transition with sequence < 2 fails
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderStateHistory.CreateTransition(
                Guid.NewGuid(),
                orderId,
                2,
                SurveyOrderStatus.AwaitingAppointment,
                SurveyOrderStatus.AwaitingDeposit,
                actorId: Guid.NewGuid(),
                source: "System",
                reason: "Appointment confirmed",
                evidenceReference: "ref-1",
                correlationId: correlationId,
                predecessorHistoryId: initial.Id,
                createdAt: Now.AddMinutes(5),
                transitionSequence: 1));

        // Legacy history without sequence (null) succeeds
        var legacyHistory = OrderStateHistory.CreateInitial(
            Guid.NewGuid(),
            orderId,
            1,
            SurveyOrderStatus.AwaitingBaselineAppointment,
            actorId: null,
            source: "Legacy",
            reason: "Legacy initial",
            correlationId: correlationId,
            createdAt: Now);

        Assert.Null(legacyHistory.TransitionSequence);
    }

    [Fact]
    public void SurveyDepositObligationEnforcesInvariantsAndAllocation()
    {
        var orderId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();

        // Rejects non-positive or fractional amount
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositObligation.Create(
                obligationId, orderId, TenantId, FarmId, PolicyVersionId, DepositPolicyId,
                0m, "VND", Now));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositObligation.Create(
                obligationId, orderId, TenantId, FarmId, PolicyVersionId, DepositPolicyId,
                100.5m, "VND", Now));

        // Rejects non-VND currency
        Assert.Throws<ArgumentException>(() =>
            SurveyDepositObligation.Create(
                obligationId, orderId, TenantId, FarmId, PolicyVersionId, DepositPolicyId,
                5_000m, "USD", Now));

        var obligation = SurveyDepositObligation.Create(
            obligationId, orderId, TenantId, FarmId, PolicyVersionId, DepositPolicyId,
            5_000m, "VND", Now);

        Assert.Equal(5_000m, obligation.Amount);
        Assert.Equal(0m, obligation.AllocatedAmount);
        Assert.Equal(5_000m, obligation.RemainingAmount);
        Assert.False(obligation.IsSatisfied);
        Assert.Equal(SurveyDepositObligationStatus.Pending, obligation.Status);

        // Partial allocation
        obligation.ApplyAllocation(2_000m, Now.AddMinutes(10));
        Assert.Equal(2_000m, obligation.AllocatedAmount);
        Assert.Equal(3_000m, obligation.RemainingAmount);
        Assert.False(obligation.IsSatisfied);
        Assert.Equal(SurveyDepositObligationStatus.Pending, obligation.Status);

        // Full allocation
        obligation.ApplyAllocation(3_000m, Now.AddMinutes(20));
        Assert.Equal(5_000m, obligation.AllocatedAmount);
        Assert.Equal(0m, obligation.RemainingAmount);
        Assert.True(obligation.IsSatisfied);
        Assert.Equal(SurveyDepositObligationStatus.Satisfied, obligation.Status);

        // Cancelled obligation rejects further allocation
        var obligationToCancel = SurveyDepositObligation.Create(
            Guid.NewGuid(), orderId, TenantId, FarmId, PolicyVersionId, DepositPolicyId,
            5_000m, "VND", Now);
        obligationToCancel.Cancel(Now.AddMinutes(5));
        Assert.Equal(SurveyDepositObligationStatus.Cancelled, obligationToCancel.Status);

        Assert.Throws<InvalidOperationException>(() =>
            obligationToCancel.ApplyAllocation(1_000m, Now.AddMinutes(10)));
    }

    [Fact]
    public void SurveyFundingReceiptAndAllocationEnforcePositiveWholeVnd()
    {
        var orderId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        var receipt = SurveyFundingReceipt.Create(
            receiptId,
            TenantId,
            orderId,
            obligationId,
            "SePay",
            "TX-998877",
            5_000m,
            "VND",
            "REF-ORDER-123",
            "receipt-fingerprint-abc",
            Now,
            Now);

        Assert.Equal(receiptId, receipt.Id);
        Assert.Equal("SePay", receipt.Provider);
        Assert.Equal("TX-998877", receipt.ProviderTransactionId);
        Assert.Equal(5_000m, receipt.Amount);

        var allocation = SurveyFundingAllocation.Create(
            Guid.NewGuid(),
            receiptId,
            orderId,
            obligationId,
            5_000m,
            "VND",
            Now.AddMinutes(1));

        Assert.Equal(5_000m, allocation.AllocatedAmount);
        Assert.Equal("VND", allocation.Currency);
    }

    [Fact]
    public void CancellationDecisionEnforcesArithmeticInvariants()
    {
        var orderId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var decisionActorId = Guid.NewGuid();
        var inputEvidenceId = Guid.NewGuid();

        // 1. Violation of R >= F + A
        var ex1 = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
                CancellationPolicyId, 1, "CUSTOMER_REQUEST", "Changed mind",
                confirmedReceiptsAmount: 5_000m,
                completedRefundsAmount: 4_000m,
                activeRefundReservationsAmount: 2_000m, // F + A = 6000 > R (5000)
                pinnedDepositAmount: 5_000m,
                retainedAmount: 0m,
                refundAmount: 0m,
                currency: "VND",
                payloadFingerprint: "fingerprint-cancel",
                createdAt: Now));
        Assert.Equal(CancellationDomainErrorCodes.ReceiptsLessThanRefundsAndReservations, ex1.Code);

        // 2. Full return before capture: R = 5000, F = 0, A = 0 => U = 5000. Retention = 0, Refund = 5000
        var fullReturn = SurveyCancellationDecision.Create(
            Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
            CancellationPolicyId, 1, "BEFORE_CAPTURE_CANCEL", "Weather unsuitable",
            confirmedReceiptsAmount: 5_000m,
            completedRefundsAmount: 0m,
            activeRefundReservationsAmount: 0m,
            pinnedDepositAmount: 5_000m,
            retainedAmount: 0m,
            refundAmount: 5_000m,
            currency: "VND",
            payloadFingerprint: "fingerprint-full-return",
            createdAt: Now);
        Assert.Equal(0m, fullReturn.RetainedAmount);
        Assert.Equal(5_000m, fullReturn.RefundAmount);

        // 3. Acceptable capture cancellation without evidence reference fails
        var ex2 = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
                CancellationPolicyId, 1, "AFTER_CAPTURE_CANCEL", "Customer decided not to proceed",
                confirmedReceiptsAmount: 10_000m,
                completedRefundsAmount: 0m,
                activeRefundReservationsAmount: 0m,
                pinnedDepositAmount: 5_000m,
                retainedAmount: 5_000m,
                refundAmount: 5_000m,
                currency: "VND",
                payloadFingerprint: "fingerprint-no-evidence",
                createdAt: Now,
                inputApprovalEvidenceId: null));
        Assert.Equal(CancellationDomainErrorCodes.RetentionRequiresAcceptableCapture, ex2.Code);

        // 4. Retention exceeding min(D, U) fails
        // R = 3000, F = 0, A = 0 => U = 3000. D = 5000. Max retention = min(5000, 3000) = 3000. Retained 4000 fails.
        var ex3 = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
                CancellationPolicyId, 1, "AFTER_CAPTURE_CANCEL", "Customer decided not to proceed",
                confirmedReceiptsAmount: 3_000m,
                completedRefundsAmount: 0m,
                activeRefundReservationsAmount: 0m,
                pinnedDepositAmount: 5_000m,
                retainedAmount: 4_000m,
                refundAmount: 0m,
                currency: "VND",
                payloadFingerprint: "fingerprint-excess-retention",
                createdAt: Now,
                inputApprovalEvidenceId: inputEvidenceId));
        Assert.Equal(CancellationDomainErrorCodes.RetentionExceedsAllowedCap, ex3.Code);

        // 5. Refund amount mismatch: U = 10000 - 0 - 0 = 10000. Retained = 5000. Expected refund = 5000. Giving 4000 fails.
        var ex4 = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
                CancellationPolicyId, 1, "AFTER_CAPTURE_CANCEL", "Customer decided not to proceed",
                confirmedReceiptsAmount: 10_000m,
                completedRefundsAmount: 0m,
                activeRefundReservationsAmount: 0m,
                pinnedDepositAmount: 5_000m,
                retainedAmount: 5_000m,
                refundAmount: 4_000m,
                currency: "VND",
                payloadFingerprint: "fingerprint-wrong-refund",
                createdAt: Now,
                inputApprovalEvidenceId: inputEvidenceId));
        Assert.Equal(CancellationDomainErrorCodes.InvalidRefundAmount, ex4.Code);

        // 6. Valid acceptable capture cancellation with retention and refund
        var validRetained = SurveyCancellationDecision.Create(
            Guid.NewGuid(), orderId, 2, requesterId, decisionActorId,
            CancellationPolicyId, 1, "AFTER_CAPTURE_CANCEL", "Customer decided not to proceed",
            confirmedReceiptsAmount: 10_000m,
            completedRefundsAmount: 0m,
            activeRefundReservationsAmount: 0m,
            pinnedDepositAmount: 5_000m,
            retainedAmount: 5_000m,
            refundAmount: 5_000m,
            currency: "VND",
            payloadFingerprint: "fingerprint-valid-retained",
            createdAt: Now,
            inputApprovalEvidenceId: inputEvidenceId);

        Assert.Equal(5_000m, validRetained.RetainedAmount);
        Assert.Equal(5_000m, validRetained.RefundAmount);
    }

    [Fact]
    public void SurveyRefundObligationFollowsStrictLifecycle()
    {
        var orderId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var refund = SurveyRefundObligation.Create(
            Guid.NewGuid(),
            orderId,
            TenantId,
            SurveyRefundOriginKind.CancellationReturn,
            decisionId,
            5_000m,
            "VND",
            Now);

        Assert.Equal(SurveyRefundObligationStatus.Pending, refund.Status);

        // Pending -> Reserved
        refund.MarkReserved(Now.AddMinutes(5));
        Assert.Equal(SurveyRefundObligationStatus.Reserved, refund.Status);

        // Reserved -> Executing
        refund.MarkExecuting(Now.AddMinutes(10));
        Assert.Equal(SurveyRefundObligationStatus.Executing, refund.Status);

        // Executing -> Completed
        refund.MarkCompleted(Now.AddMinutes(15));
        Assert.Equal(SurveyRefundObligationStatus.Completed, refund.Status);

        // Completed is terminal - cannot execute or cancel
        var ex = Assert.Throws<SurveyDomainException>(() =>
            refund.Cancel("No longer needed", Now.AddMinutes(20)));
        Assert.Equal(CancellationDomainErrorCodes.RefundObligationAlreadyFinal, ex.Code);
    }

    [Fact]
    public void CancellationRetentionStoresValidClassificationFact()
    {
        var orderId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();

        var retention = CancellationRetention.Create(
            Guid.NewGuid(),
            orderId,
            TenantId,
            decisionId,
            5_000m,
            "VND",
            "Customer cancelled post acceptable capture",
            Now);

        Assert.Equal(5_000m, retention.Amount);
        Assert.Equal("VND", retention.Currency);
        Assert.Equal("Customer cancelled post acceptable capture", retention.Reason);
    }

    [Fact]
    public void CaptureInputManifestAndApprovalEnforceIntegrity()
    {
        var orderId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var fingerprint = "sha256-abc123456789";

        // Rejects non-positive file count
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaptureInputManifest.Create(
                manifestId, TenantId, FarmId, orderId, attemptId, missionId,
                fingerprint, totalFileCount: 0, totalSizeBytes: 1024, createdAt: Now));

        // Rejects non-positive size bytes
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaptureInputManifest.Create(
                manifestId, TenantId, FarmId, orderId, attemptId, missionId,
                fingerprint, totalFileCount: 50, totalSizeBytes: 0, createdAt: Now));

        var manifest = CaptureInputManifest.Create(
            manifestId, TenantId, FarmId, orderId, attemptId, missionId,
            fingerprint, totalFileCount: 150, totalSizeBytes: 1024 * 1024 * 500, createdAt: Now);

        Assert.Equal(CaptureInputManifestStatus.Uploaded, manifest.Status);
        Assert.Equal(150, manifest.TotalFileCount);

        manifest.MarkApproved(Now.AddMinutes(10));
        Assert.Equal(CaptureInputManifestStatus.Approved, manifest.Status);

        // Approved manifest cannot be superseded
        Assert.Throws<InvalidOperationException>(() =>
            manifest.MarkSuperseded(Now.AddMinutes(15)));

        // Input Approval
        var jobId = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var approval = CaptureInputApproval.Create(
            Guid.NewGuid(),
            TenantId,
            FarmId,
            orderId,
            attemptId,
            missionId,
            manifestId,
            fingerprint,
            jobId,
            approvedBy,
            Now.AddMinutes(10),
            "Images meet sharpness and overlap thresholds");

        Assert.Equal(jobId, approval.ProcessingJobId);
        Assert.Equal(approvedBy, approval.ApprovedBy);
        Assert.Equal(fingerprint, approval.ManifestFingerprint);
    }

}
