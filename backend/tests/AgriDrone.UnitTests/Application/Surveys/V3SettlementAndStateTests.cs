using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

/// <summary>
/// Unit/Domain tests for V3-05D/E/F settlement and state machine transitions:
/// - TC-F07-U01: MAP 150 poles * 1,000 VND - 100,000 deposit = 50,000 due; invoice correction keeps pinned rates &amp; invalidates old checkout.
/// - TC-ST-ORD-06: SurveyOrder Processing -> AwaitingSettlement (V3-05D freeze).
/// - TC-ST-PAY-02: SurveyPayment Pending/Processing -> Invalidated (V3-05E invoice correction).
/// - TC-ST-NEG-04: Legacy completion/pricing shortcuts blocked on V3 orders.
/// </summary>
public sealed class V3SettlementAndStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact, Trait("TC", "TC-F07-U01"), Trait("TC", "TC-ST-PAY-02")]
    public void InvoiceCorrectionPreservesPinnedPriceAndDepositAndInvalidatesOldCheckouts()
    {
        var order = CreateV3MapOrder(pricePerPole: 1_000m, depositAmount: 100_000m);
        var rev1Id = Guid.NewGuid();
        var rev2Id = Guid.NewGuid();
        var approvedId = Guid.NewGuid();
        var snapshot1Id = Guid.NewGuid();
        var snapshot2Id = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        // Revision 1: 150 poles * 1,000 - 100,000 deposit = 50,000 due
        var rev1 = CreateInvoiceRevision(
            rev1Id, order.Id, 1, null, approvedId, snapshot1Id,
            poleCount: 150, priceId, pricePerPole: 1_000m, policyId, depositNet: 100_000m,
            "Initial invoice", Now);

        Assert.Equal(150_000m, rev1.TotalAmount);
        Assert.Equal(100_000m, rev1.ValidAllocatedDepositNet);
        Assert.Equal(50_000m, rev1.DueAmount);
        Assert.Equal(0m, rev1.OverpaymentAmount);

        // Open FinalBalance checkout created for rev1
        var checkout1 = SurveyPayment.Create(
            order.Id,
            Money.Create(rev1.DueAmount, CurrencyCode.Vnd),
            "SEPAY",
            Now.AddMinutes(1),
            PaymentPurpose.FinalBalance);
        Assert.True(checkout1.BindInvoiceRevision(rev1.Id).IsSuccess);
        checkout1.StartProcessing("SP-OLD-01", Now.AddMinutes(2));
        Assert.Equal(SurveyPaymentStatus.Processing, checkout1.Status);

        // Pre-release correction: pole count corrected to 140, pinned price (1,000) & deposit (100,000) preserved
        var rev2 = CreateInvoiceRevision(
            rev2Id, order.Id, 2, rev1.Id, approvedId, snapshot2Id,
            poleCount: 140, priceId, pricePerPole: 1_000m, policyId, depositNet: 100_000m,
            "Corrected pole count after review", Now.AddMinutes(5));

        Assert.True(rev1.Supersede(ActorId, Now.AddMinutes(5), "Pole count corrected", rev2.Id).IsSuccess);
        Assert.Equal(SurveyInvoiceRevision.StatusSuperseded, rev1.Status);
        Assert.Equal(rev2.Id, rev1.SuccessorRevisionId);

        // TC-ST-PAY-02: Old FinalBalance checkout is Invalidated
        var invRes = checkout1.InvalidateForInvoiceCorrection(
            ActorId, Now.AddMinutes(5), "Pre-release invoice correction", rev2.Id);
        Assert.True(invRes.IsSuccess, invRes.Error?.Description);
        Assert.Equal(SurveyPaymentStatus.Invalidated, checkout1.Status);
        Assert.Equal(rev2.Id, checkout1.SupersededByInvoiceRevisionId);

        // Idempotent replay of invalidation succeeds; different metadata conflicts
        Assert.True(checkout1.InvalidateForInvoiceCorrection(
            ActorId, Now.AddMinutes(5), "Pre-release invoice correction", rev2.Id).IsSuccess);
        var conflictInv = checkout1.InvalidateForInvoiceCorrection(
            ActorId, Now.AddMinutes(6), "Different reason", rev2.Id);
        Assert.True(conflictInv.IsFailure);
        Assert.Equal("SurveyPayment.AlreadyInvalidated", conflictInv.Error.Code);

        // New checkout reflects updated DueAmount (40,000 VND) with same pinned deposit & price
        Assert.Equal(140_000m, rev2.TotalAmount);
        Assert.Equal(100_000m, rev2.ValidAllocatedDepositNet);
        Assert.Equal(40_000m, rev2.DueAmount);
    }

    [Fact, Trait("TC", "TC-ST-PAY-02")]
    public void AlreadyPaidCheckoutCannotBeInvalidatedByInvoiceCorrection()
    {
        var payment = SurveyPayment.Create(
            Guid.NewGuid(),
            Money.Create(50_000m, CurrencyCode.Vnd),
            "SEPAY",
            Now,
            PaymentPurpose.FinalBalance);
        payment.StartProcessing("SP-PAID-01", Now.AddMinutes(1));
        payment.Confirm("SP-PAID-01", Now.AddMinutes(2));
        Assert.Equal(SurveyPaymentStatus.Paid, payment.Status);

        var res = payment.InvalidateForInvoiceCorrection(
            ActorId, Now.AddMinutes(5), "Attempted invalidation after payment", Guid.NewGuid());
        Assert.True(res.IsFailure);
        Assert.Equal("SurveyPayment.CannotInvalidate", res.Error.Code);
        Assert.Equal(SurveyPaymentStatus.Paid, payment.Status);
    }

    [Fact, Trait("TC", "TC-ST-ORD-06")]
    public void V3OrderTransitionsFromProcessingToAwaitingSettlementOnFreeze()
    {
        var order = CreateV3MapOrder(pricePerPole: 1_000m, depositAmount: 100_000m);
        order.TransitionToAwaitingDepositV3(Now.AddMinutes(1));
        order.TransitionToReadyForCaptureV3(Now.AddMinutes(2));
        order.TransitionToCapturingV3(Now.AddMinutes(3));
        order.TransitionToAwaitingCaptureUploadV3(Now.AddMinutes(4));
        order.TransitionToAwaitingInputReviewV3(Now.AddMinutes(5));
        order.TransitionToAwaitingProcessingV3(Now.AddMinutes(6));
        order.TransitionToProcessingV3(Now.AddMinutes(7));

        var snapshot = SurveyOrderPoleCountSnapshot.Create(
            Guid.NewGuid(), order.Id, order.TenantId, order.FarmId,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), [], ActorId, Now.AddMinutes(8));

        order.RecordInitialMapApproval(snapshot, order.Version);

        Assert.Equal(SurveyOrderStatus.Processing, order.Status);
        Assert.Equal(snapshot.Id, order.VerifiedPoleCountSnapshotId);
        Assert.Equal(snapshot.FarmBaseMapVersionId, order.BaselineMapVersionId);

        order.TransitionToAwaitingSettlementV3(Now.AddMinutes(9));
        Assert.Equal(SurveyOrderStatus.AwaitingSettlement, order.Status);
    }

    [Fact, Trait("TC", "TC-ST-NEG-04")]
    public void V3OrderBlocksLegacyResultAndPricingBypassBeforeSettlement()
    {
        var order = CreateV3MapOrder(pricePerPole: 1_000m, depositAmount: 100_000m);

        Assert.Throws<InvalidOperationException>(() =>
            order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 1_000m));
        Assert.Throws<SurveyDomainException>(() =>
            order.ConfirmPricing(Guid.NewGuid(), PricePerPole.Create(1_000m), CurrencyCode.Vnd, ActorId, Now));
        Assert.Throws<SurveyDomainException>(() => order.MarkPaymentConfirmed(Now));
        Assert.Throws<SurveyDomainException>(() => order.Complete(Now));

        // Direct V3 transition from ReadyForCapture -> Completed without Processing/AwaitingSettlement is blocked
        order.TransitionToAwaitingDepositV3(Now.AddMinutes(1));
        order.TransitionToReadyForCaptureV3(Now.AddMinutes(2));
        var ex = Assert.Throws<SurveyDomainException>(() => order.TransitionToCompletedV3(Now.AddMinutes(3)));
        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, ex.Code);
        Assert.Equal(SurveyOrderStatus.ReadyForCapture, order.Status);
    }

    private static SurveyOrder CreateV3MapOrder(decimal pricePerPole, decimal depositAmount)
    {
        var snapshot = new ResolvedSurveyCommercialSnapshot(
            BusinessPolicyVersionId: Guid.NewGuid(),
            PolicyFingerprint: new string('f', 64),
            SurveyServiceRevisionId: Guid.NewGuid(),
            SurveyServiceRevisionVersion: 1,
            ServiceCode: "MAP",
            MapsPoles: true,
            ScreensDisease: false,
            PriceVersionId: Guid.NewGuid(),
            PriceVersionNumber: 1,
            PricePerPole: pricePerPole,
            Currency: "VND",
            DepositPolicyId: Guid.NewGuid(),
            DepositVersionNumber: 1,
            DepositAmount: depositAmount,
            ClaimPolicyId: Guid.NewGuid(),
            ClaimPolicyVersionNumber: 1,
            ClaimWindowDays: 7,
            ClaimTimeZoneId: "Asia/Ho_Chi_Minh",
            ExcludeNoticeDate: true,
            CancellationPolicyId: Guid.NewGuid(),
            CancellationPolicyVersionNumber: 1,
            RefundableBeforeCapture: true,
            RetainDepositAfterCapture: true,
            AllowedFreeNoShowReschedules: 1,
            EvaluatedAt: Now);

        return SurveyOrder.CreateV3CaptureOrder(
            "ORD-V3-SETTLE", TenantId, FarmId, Guid.NewGuid(), Guid.NewGuid(),
            snapshot, requiresBaselineMapping: true, createdAt: Now);
    }

    private static SurveyInvoiceRevision CreateInvoiceRevision(
        Guid id,
        Guid orderId,
        int revisionNumber,
        Guid? predecessorRevisionId,
        Guid approvedResultRevisionId,
        Guid poleCountSnapshotId,
        int poleCount,
        Guid servicePriceId,
        decimal pricePerPole,
        Guid policyId,
        decimal depositNet,
        string reason,
        DateTimeOffset finalizedAt)
    {
        var total = poleCount * pricePerPole;
        var due = Math.Max(0m, total - depositNet);
        var overpayment = Math.Max(0m, depositNet - total);

        var fp = SurveyInvoiceRevision.ComputeEvidenceFingerprint(
            TenantId, orderId, FarmId, revisionNumber,
            approvedResultRevisionId, poleCountSnapshotId, poleCount,
            servicePriceId, pricePerPole, "VND", policyId,
            total, depositNet, due, overpayment,
            ActorId, finalizedAt, reason, predecessorRevisionId,
            null, null, null);

        var res = SurveyInvoiceRevision.Create(
            id: id,
            tenantId: TenantId,
            farmId: FarmId,
            surveyOrderId: orderId,
            revisionNumber: revisionNumber,
            approvedResultRevisionId: approvedResultRevisionId,
            poleCountSnapshotId: poleCountSnapshotId,
            verifiedBillablePoleCount: poleCount,
            surveyServicePriceId: servicePriceId,
            pricePerPole: pricePerPole,
            currency: "VND",
            commercialPolicyVersionId: policyId,
            validAllocatedDepositNet: depositNet,
            finalizedBy: ActorId,
            finalizedAt: finalizedAt,
            finalizationReason: reason,
            evidenceFingerprint: fp,
            predecessorRevisionId: predecessorRevisionId);

        Assert.True(res.IsSuccess, res.Error?.Description);
        return res.Value;
    }
}
