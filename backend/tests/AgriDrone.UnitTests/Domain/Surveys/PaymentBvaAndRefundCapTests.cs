using System.Globalization;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

/// <summary>
/// BVA tests for:
/// - TC-F07-U02: 7-calendar-day claim deadline in UTC+7 (notice 14:00 10/11 -> deadline 00:00 18/11 UTC+7 = 2026-11-17T17:00:00Z).
/// - TC-F07-U03: Order refund cap at exact received amount (150,000 VND) vs 1 VND over (150,001 VND).
/// </summary>
public sealed class PaymentBvaAndRefundCapTests
{
    // 14:00:00 +07:00 on 2026-11-10 == 07:00:00Z on 2026-11-10
    private static readonly DateTimeOffset NoticeUtc =
        new(2026, 11, 10, 7, 0, 0, TimeSpan.Zero);

    // 00:00:00 +07:00 on 2026-11-18 == 17:00:00Z on 2026-11-17 (exclusive)
    private static readonly DateTimeOffset ExpectedDeadlineUtc =
        DateTimeOffset.Parse("2026-11-17T17:00:00Z", CultureInfo.InvariantCulture);

    // ─── TC-F07-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F07-U02")]
    public void SevenDayClaimDeadlineMatches0000Nov18UtcPlus7AndEnforcesBoundary()
    {
        var deadline = ClaimPolicy.CalculateSevenDayDeadline(NoticeUtc);
        Assert.Equal(ExpectedDeadlineUtc, deadline);

        var policy = ClaimPolicy.Create(
            id: Guid.NewGuid(),
            versionNumber: 1,
            predecessorVersionId: null,
            calendarWindowDays: 7,
            timeZoneId: "Asia/Ho_Chi_Minh",
            excludeNoticeDate: true,
            effectiveFrom: NoticeUtc.AddDays(-10),
            effectiveTo: null,
            createdBy: Guid.NewGuid(),
            createdAt: NoticeUtc.AddDays(-10),
            reasonCode: "INITIAL");

        // Just before 00:00 18/11 UTC+7 (23:59:59 17/11 UTC+7) -> eligible
        Assert.True(policy.IsClaimEligible(NoticeUtc, ExpectedDeadlineUtc.AddSeconds(-1)));

        // Exactly at 00:00 18/11 UTC+7 -> expired (exclusive)
        Assert.False(policy.IsClaimEligible(NoticeUtc, ExpectedDeadlineUtc));
    }

    // ─── TC-F07-U03 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F07-U03")]
    public void RefundCapAllowsExactReceivedAmountAndBlocksOneVndOver()
    {
        var terms = new PinnedCancellationPolicyTerms(
            Guid.NewGuid(),
            VersionNumber: 1,
            RefundableBeforeCapture: true,
            RetainDepositAfterCapture: true,
            AllowedFreeNoShowReschedules: 1,
            OperationalPostponementRefundable: true,
            ServiceFailureRefundable: true);

        const decimal orderReceived = 150_000m;
        var now = NoticeUtc;

        // Exact cap (150,000 VND against 150,000 VND received) -> allowed
        var decision = SurveyCancellationDecision.Create(
            id: Guid.NewGuid(),
            surveyOrderId: Guid.NewGuid(),
            orderVersion: 1,
            requesterId: Guid.NewGuid(),
            decisionActorId: Guid.NewGuid(),
            policyTerms: terms,
            decisionKind: CancellationDecisionKind.CustomerBeforeCapture,
            reasonCode: "PRE_FLIGHT_CANCEL",
            reasonDescription: "Customer cancelled before flight",
            confirmedReceiptsAmount: orderReceived,
            completedRefundsAmount: 0m,
            activeRefundReservationsAmount: 0m,
            pinnedDepositAmount: 100_000m,
            retainedAmount: 0m,
            refundAmount: 150_000m,
            currency: "VND",
            payloadFingerprint: "cap-exact-150k",
            createdAt: now);

        Assert.Equal(150_000m, decision.RefundAmount);

        // 1 VND over cap (150,001 total with 100k already completed + 50,001 active reservation) -> blocked
        var capEx = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                id: Guid.NewGuid(),
                surveyOrderId: Guid.NewGuid(),
                orderVersion: 1,
                requesterId: Guid.NewGuid(),
                decisionActorId: Guid.NewGuid(),
                policyTerms: terms,
                decisionKind: CancellationDecisionKind.CustomerBeforeCapture,
                reasonCode: "OVER_CAP",
                reasonDescription: "Exceeds received amount by 1 VND",
                confirmedReceiptsAmount: orderReceived,
                completedRefundsAmount: 100_000m,
                activeRefundReservationsAmount: 50_001m,
                pinnedDepositAmount: 100_000m,
                retainedAmount: 0m,
                refundAmount: 0m,
                currency: "VND",
                payloadFingerprint: "cap-over-1",
                createdAt: now));
        Assert.Equal(CancellationDomainErrorCodes.ReceiptsLessThanRefundsAndReservations, capEx.Code);

        // Requesting 150,001 refund when unreserved is 150,000 -> blocked
        var refundEx = Assert.Throws<SurveyDomainException>(() =>
            SurveyCancellationDecision.Create(
                id: Guid.NewGuid(),
                surveyOrderId: Guid.NewGuid(),
                orderVersion: 1,
                requesterId: Guid.NewGuid(),
                decisionActorId: Guid.NewGuid(),
                policyTerms: terms,
                decisionKind: CancellationDecisionKind.CustomerBeforeCapture,
                reasonCode: "OVER_UNRESERVED",
                reasonDescription: "Refund 150,001 exceeds 150,000",
                confirmedReceiptsAmount: orderReceived,
                completedRefundsAmount: 0m,
                activeRefundReservationsAmount: 0m,
                pinnedDepositAmount: 100_000m,
                retainedAmount: 0m,
                refundAmount: 150_001m,
                currency: "VND",
                payloadFingerprint: "cap-over-unreserved",
                createdAt: now));
        Assert.Equal(CancellationDomainErrorCodes.InvalidRefundAmount, refundEx.Code);
    }

    [Fact, Trait("TC", "TC-F07-U03")]
    public void QualityRefundApproveAcceptsPositiveWholeVndAndRejectsNonPositive()
    {
        var now = new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);
        var (order, result) = BuildPublishedCompletedOrder(now);
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var refundCase = QualityRefundCase.Report(
            order, result, ownerId,
            issues: [new ReportedPoleIssue("P-01", "FALSE_POSITIVE", "Tree misidentified as diseased")],
            description: "Pole P-01 incorrectly flagged",
            evidenceReference: "photo-p01",
            recipientBankName: "Bank A",
            recipientAccountNumber: "1234567890",
            recipientAccountName: "NGUYEN VAN A",
            now: now.AddHours(13));

        refundCase.VerifyByManager(
            managerId,
            findings: [new VerifiedPoleIssue("P-01", ConfirmedError: true, "Field check confirms healthy tree")],
            reason: "Cross-checked on-site evidence",
            now: now.AddHours(13).AddMinutes(30),
            expectedVersion: refundCase.Version);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            refundCase.Approve(adminId, "Invalid zero", 0m, now.AddHours(14)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            refundCase.Approve(adminId, "Invalid negative", -1m, now.AddHours(14)));

        refundCase.Approve(adminId, "Approved 150k", 150_000m, now.AddHours(14));
        Assert.Equal(QualityRefundStatus.Approved, refundCase.Status);
        Assert.Equal(150_000m, refundCase.ApprovedAmount);
    }

    private static (SurveyOrder Order, SurveyResult Result) BuildPublishedCompletedOrder(
        DateTimeOffset now)
    {
        var order = SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            null,
            "test.v1",
            "NO_PREVIOUS_PUBLISHED_ORDER",
            now);

        var managerId = Guid.NewGuid();

        order.MarkBaselineAppointmentConfirmed(now.AddMinutes(1));
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 5_000m, now.AddMinutes(5));
        order.StartBaselineMapping(now.AddHours(1));
        order.MarkBaselineCandidatesReady(now.AddHours(2));
        order.SetFarmBoundaryFromOrthophoto(Guid.NewGuid(), managerId, now.AddHours(3));
        order.RecordBaselinePublication(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(80), managerId, now.AddHours(4));
        order.ConfirmPricing(Guid.NewGuid(), PricePerPole.Create(20_000m), CurrencyCode.Vnd, managerId, now.AddHours(5));
        order.MarkPaidAppointmentConfirmed(now.AddHours(6));
        order.MarkPaymentConfirmed(now.AddHours(7));
        order.StartPaidService(now.AddHours(8));
        order.MarkAnalysisReady(now.AddHours(9));
        order.Complete(now.AddHours(10));

        using var provenance = JsonDocument.Parse("{}");
        var result = SurveyResult.CreateForManagerReview(
            order.Id, order.TenantId, order.FarmId,
            SurveyServiceType.PlantHealth, provenance,
            now.AddHours(11));
        result.Approve(managerId, now.AddHours(12));
        result.Publish(managerId, now.AddHours(12).AddMinutes(1));

        return (order, result);
    }
}
