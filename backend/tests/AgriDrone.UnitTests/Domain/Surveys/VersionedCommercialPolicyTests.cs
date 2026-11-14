using System.Globalization;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class VersionedCommercialPolicyTests
{
    private static readonly DateTimeOffset BaseUtc =
        new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public void SurveyServiceRevisionValidInputCreatesSuccessfully()
    {
        var id = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var revision = SurveyServiceRevision.Create(
            id: id,
            surveyServiceId: serviceId,
            versionNumber: 1,
            predecessorVersionId: null,
            name: "V3 Baseline Mapping",
            description: "High-resolution orthophoto mapping",
            mapsPoles: true,
            screensDisease: false,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "INITIAL_PROVISIONING",
            evidenceReference: "DOC-2026-001");

        Assert.Equal(id, revision.Id);
        Assert.Equal(serviceId, revision.SurveyServiceId);
        Assert.Equal(1, revision.VersionNumber);
        Assert.Null(revision.PredecessorVersionId);
        Assert.Equal("V3 Baseline Mapping", revision.Name);
        Assert.True(revision.MapsPoles);
        Assert.False(revision.ScreensDisease);
        Assert.True(revision.IsEffectiveAt(BaseUtc));
        Assert.True(revision.IsEffectiveAt(BaseUtc.AddDays(10)));
    }

    [Fact]
    public void SurveyServiceRevisionMapsPolesFalseThrowsArgumentException()
    {
        var id = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Neither capability
        var exNoCaps = Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: id,
                surveyServiceId: serviceId,
                versionNumber: 1,
                predecessorVersionId: null,
                name: "Invalid Service",
                description: "No capabilities",
                mapsPoles: false,
                screensDisease: false,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));

        Assert.Contains("MapsPoles", exNoCaps.Message, StringComparison.OrdinalIgnoreCase);

        // Disease-only (mapsPoles: false, screensDisease: true) is strictly rejected in V3
        var exDiseaseOnly = Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: Guid.NewGuid(),
                surveyServiceId: serviceId,
                versionNumber: 1,
                predecessorVersionId: null,
                name: "Disease Only Service",
                description: "Cannot screen disease without pole mapping",
                mapsPoles: false,
                screensDisease: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));

        Assert.Contains("MapsPoles", exDiseaseOnly.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SurveyServiceRevisionInvalidLineageThrowsExpectedExceptions()
    {
        var id = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var predecessorId = Guid.NewGuid();

        // Version 1 cannot have predecessor
        Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: id,
                surveyServiceId: serviceId,
                versionNumber: 1,
                predecessorVersionId: predecessorId,
                name: "Service",
                description: "Description",
                mapsPoles: true,
                screensDisease: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));

        // Version > 1 must have predecessor
        Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: id,
                surveyServiceId: serviceId,
                versionNumber: 2,
                predecessorVersionId: null,
                name: "Service",
                description: "Description",
                mapsPoles: true,
                screensDisease: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));

        // Predecessor cannot be self
        Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: id,
                surveyServiceId: serviceId,
                versionNumber: 2,
                predecessorVersionId: id,
                name: "Service",
                description: "Description",
                mapsPoles: true,
                screensDisease: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));

        // Predecessor cannot be empty GUID
        Assert.Throws<ArgumentException>(() =>
            SurveyServiceRevision.Create(
                id: id,
                surveyServiceId: serviceId,
                versionNumber: 2,
                predecessorVersionId: Guid.Empty,
                name: "Service",
                description: "Description",
                mapsPoles: true,
                screensDisease: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "TEST"));
    }

    [Fact]
    public void SurveyServiceRevisionHalfOpenWindowAndCloseAtWorksCorrectly()
    {
        var id = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var start = BaseUtc;
        var end = BaseUtc.AddDays(7);

        var revision = SurveyServiceRevision.Create(
            id: id,
            surveyServiceId: serviceId,
            versionNumber: 1,
            predecessorVersionId: null,
            name: "MAP Service",
            description: "Pole mapping",
            mapsPoles: true,
            screensDisease: false,
            effectiveFrom: start,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: start,
            reasonCode: "INIT");

        Assert.False(revision.IsEffectiveAt(start.AddSeconds(-1)));
        Assert.True(revision.IsEffectiveAt(start));
        Assert.True(revision.IsEffectiveAt(start.AddDays(3)));

        revision.CloseAt(end);

        Assert.True(revision.IsEffectiveAt(start));
        Assert.True(revision.IsEffectiveAt(end.AddSeconds(-1)));
        Assert.False(revision.IsEffectiveAt(end)); // Half-open: [from, to)
        Assert.False(revision.IsEffectiveAt(end.AddSeconds(1)));

        // Cannot close already closed window
        var exClosed = Assert.Throws<SurveyDomainException>(() => revision.CloseAt(end.AddDays(1)));
        Assert.Equal(PolicyDomainErrorCodes.WindowAlreadyClosed, exClosed.Code);

        // Cannot close with end before start
        var openRevision = SurveyServiceRevision.Create(
            id: Guid.NewGuid(),
            surveyServiceId: serviceId,
            versionNumber: 1,
            predecessorVersionId: null,
            name: "MAP Service",
            description: "Pole mapping",
            mapsPoles: true,
            screensDisease: false,
            effectiveFrom: start,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: start,
            reasonCode: "INIT");

        var exInvalid = Assert.Throws<SurveyDomainException>(() => openRevision.CloseAt(start.AddSeconds(-1)));
        Assert.Equal(PolicyDomainErrorCodes.InvalidEffectiveWindow, exInvalid.Code);
    }

    [Fact]
    public void SurveyDepositPolicyPositiveWholeVndValidatesProperly()
    {
        var id = Guid.NewGuid();

        // Valid positive integer VND
        var policy = SurveyDepositPolicy.Create(
            id: id,
            versionNumber: 1,
            predecessorVersionId: null,
            amount: 100000m,
            currency: CurrencyCode.Vnd,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "DEPOSIT_ESTABLISHED");

        Assert.Equal(100000m, policy.Amount);
        Assert.Equal("VND", policy.Currency);

        // Decimal fraction is rejected for V3 VND
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                amount: 100000.50m,
                currency: CurrencyCode.Vnd,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "DECIMAL_FRACTION"));

        // Zero amount is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                amount: 0m,
                currency: CurrencyCode.Vnd,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "ZERO_AMOUNT"));

        // Negative amount is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SurveyDepositPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                amount: -50000m,
                currency: CurrencyCode.Vnd,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "NEGATIVE_AMOUNT"));

        // Non-VND currency is rejected
        var usdCurrency = CurrencyCode.Create("USD");
        Assert.Throws<ArgumentException>(() =>
            SurveyDepositPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                amount: 100m,
                currency: usdCurrency,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "USD_REJECTED"));
    }

    [Fact]
    public void ClaimPolicyBrClm02AndBrClm03CalculatesExclusiveDeadlineCorrectly()
    {
        // Notice issued at 14:00 on 10/11/2026 UTC+7 (which is 07:00:00Z on 2026-11-10)
        var noticeUtc = new DateTimeOffset(2026, 11, 10, 7, 0, 0, TimeSpan.Zero);

        var policy = ClaimPolicy.Create(
            id: Guid.NewGuid(),
            versionNumber: 1,
            predecessorVersionId: null,
            calendarWindowDays: 7,
            timeZoneId: "Asia/Ho_Chi_Minh",
            excludeNoticeDate: true,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "CLAIM_POLICY_V1");

        // BR-CLM-02 & BR-CLM-03:
        // Notice on calendar date D = 10/11/2026 in Asia/Ho_Chi_Minh.
        // Window is 7 calendar days, accepted through end of 17/11/2026.
        // Exclusive deadline is 00:00:00 on 18/11/2026 UTC+7, which is 17:00:00Z on 17/11/2026.
        var deadlineUtc = policy.CalculateExclusiveDeadline(noticeUtc);

        var expectedDeadlineUtc = new DateTimeOffset(2026, 11, 17, 17, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedDeadlineUtc, deadlineUtc);

        // Verification of eligibility:
        // Before notice creation => ineligible (cannot claim before completion notice)
        var receivedBeforeNotice = noticeUtc.AddMinutes(-5);
        Assert.False(policy.IsClaimEligible(noticeUtc, receivedBeforeNotice));

        // Before deadline => eligible
        var receivedJustBefore = expectedDeadlineUtc.AddSeconds(-1);
        Assert.True(policy.IsClaimEligible(noticeUtc, receivedJustBefore));

        // Exactly at deadline => ineligible (exclusive)
        Assert.False(policy.IsClaimEligible(noticeUtc, expectedDeadlineUtc));

        // After deadline => ineligible
        var receivedAfter = expectedDeadlineUtc.AddHours(2);
        Assert.False(policy.IsClaimEligible(noticeUtc, receivedAfter));
    }

    [Fact]
    public void ClaimPolicyDynamicWindowAndExcludeNoticeToggleComputesAccurately()
    {
        var noticeUtc = new DateTimeOffset(2026, 11, 10, 7, 0, 0, TimeSpan.Zero);

        // Test with different parameters: 14 days, excludeNoticeDate = false
        var policy14Days = ClaimPolicy.Create(
            id: Guid.NewGuid(),
            versionNumber: 1,
            predecessorVersionId: null,
            calendarWindowDays: 14,
            timeZoneId: "Asia/Ho_Chi_Minh",
            excludeNoticeDate: false,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "14_DAYS_POLICY");

        // Date D = 10/11. Window = 14 days including notice date.
        // Accepts through D + 13 = 23/11.
        // Exclusive deadline is 00:00:00 on 24/11/2026 UTC+7 = 17:00:00Z on 23/11/2026.
        var deadline14Days = policy14Days.CalculateExclusiveDeadline(noticeUtc);
        var expectedDeadline14Days = new DateTimeOffset(2026, 11, 23, 17, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedDeadline14Days, deadline14Days);

        // Invalid calendar window days throws ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClaimPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                calendarWindowDays: 0,
                timeZoneId: "Asia/Ho_Chi_Minh",
                excludeNoticeDate: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "INVALID"));

        // Unrecognized timezone throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            ClaimPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                calendarWindowDays: 7,
                timeZoneId: "NonExistent/TimeZone",
                excludeNoticeDate: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "INVALID_TZ"));
    }

    [Fact]
    public void CancellationPolicyAllowedNoShowsAndRulesValidatesProperly()
    {
        var policy = CancellationPolicy.Create(
            id: Guid.NewGuid(),
            versionNumber: 1,
            predecessorVersionId: null,
            refundableBeforeCapture: true,
            retainDepositAfterCapture: true,
            allowedFreeNoShowReschedules: 1,
            operationalPostponementRefundable: true,
            serviceFailureRefundable: true,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "CANCELLATION_V1");

        Assert.True(policy.RefundableBeforeCapture);
        Assert.True(policy.RetainDepositAfterCapture);
        Assert.Equal(1, policy.AllowedFreeNoShowReschedules);
        Assert.True(policy.OperationalPostponementRefundable);
        Assert.True(policy.ServiceFailureRefundable);

        // Negative allowed no-shows throws
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CancellationPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: 1,
                predecessorVersionId: null,
                refundableBeforeCapture: true,
                retainDepositAfterCapture: true,
                allowedFreeNoShowReschedules: -1,
                operationalPostponementRefundable: true,
                serviceFailureRefundable: true,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "NEGATIVE_RESCHEDULE"));
    }

    [Fact]
    public void BusinessPolicyVersionDeterministicFingerprintAndBundleIntegrity()
    {
        var id = Guid.NewGuid();
        var depositId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var cancellationId = Guid.NewGuid();
        var rev1 = Guid.NewGuid();
        var rev2 = Guid.NewGuid();

        var bundle = BusinessPolicyVersion.Create(
            id: id,
            streamKey: "DEFAULT_COMMERCIAL_BUNDLE",
            versionNumber: 1,
            predecessorVersionId: null,
            surveyDepositPolicyId: depositId,
            claimPolicyId: claimId,
            cancellationPolicyId: cancellationId,
            serviceRevisionIds: [rev1, rev2],
            pricingCurrency: CurrencyCode.Vnd,
            effectiveFrom: BaseUtc,
            effectiveTo: null,
            createdBy: ActorId,
            createdAt: BaseUtc,
            reasonCode: "BUNDLE_V1");

        Assert.Equal(id, bundle.Id);
        Assert.Equal("DEFAULT_COMMERCIAL_BUNDLE", bundle.StreamKey);
        Assert.Contains(rev1, bundle.ServiceRevisionIds);
        Assert.Contains(rev2, bundle.ServiceRevisionIds);
        Assert.True(bundle.ContainsServiceRevision(rev1));
        Assert.False(bundle.ContainsServiceRevision(Guid.NewGuid()));
        Assert.False(string.IsNullOrWhiteSpace(bundle.PolicyFingerprint));

        // Determinism test: passing revisions in reverse order produces identical fingerprint
        var fingerprintReversed = BusinessPolicyVersion.ComputeFingerprint(
            "DEFAULT_COMMERCIAL_BUNDLE",
            1,
            depositId,
            claimId,
            cancellationId,
            [rev2, rev1],
            "VND");

        Assert.Equal(bundle.PolicyFingerprint, fingerprintReversed);

        // Changing any single dependency changes fingerprint
        var fingerprintDifferentDeposit = BusinessPolicyVersion.ComputeFingerprint(
            "DEFAULT_COMMERCIAL_BUNDLE",
            1,
            Guid.NewGuid(), // different deposit ID
            claimId,
            cancellationId,
            [rev1, rev2],
            "VND");

        Assert.NotEqual(bundle.PolicyFingerprint, fingerprintDifferentDeposit);

        // Empty service revisions rejected
        Assert.Throws<ArgumentException>(() =>
            BusinessPolicyVersion.Create(
                id: Guid.NewGuid(),
                streamKey: "BUNDLE",
                versionNumber: 1,
                predecessorVersionId: null,
                surveyDepositPolicyId: depositId,
                claimPolicyId: claimId,
                cancellationPolicyId: cancellationId,
                serviceRevisionIds: [],
                pricingCurrency: CurrencyCode.Vnd,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "EMPTY_SERVICES"));

        // Duplicate service revisions rejected
        Assert.Throws<ArgumentException>(() =>
            BusinessPolicyVersion.Create(
                id: Guid.NewGuid(),
                streamKey: "BUNDLE",
                versionNumber: 1,
                predecessorVersionId: null,
                surveyDepositPolicyId: depositId,
                claimPolicyId: claimId,
                cancellationPolicyId: cancellationId,
                serviceRevisionIds: [rev1, rev1],
                pricingCurrency: CurrencyCode.Vnd,
                effectiveFrom: BaseUtc,
                effectiveTo: null,
                createdBy: ActorId,
                createdAt: BaseUtc,
                reasonCode: "DUPLICATE_SERVICES"));
    }

    [Fact]
    public void BusinessPolicyActivationInitialAndDecisionTransitionsMaintainsProjectionAndHistory()
    {
        var bundleV1Id = Guid.NewGuid();
        var initialDecisionId = Guid.NewGuid();

        // 1. Initial creation: PublicIntakeEnabled, CaptureEnabled, ReleaseEnabled are all FALSE by default
        var (activation, initialDecision) = BusinessPolicyActivation.CreateInitial(
            activeBusinessPolicyVersionId: bundleV1Id,
            initialDecisionId: initialDecisionId,
            activatedBy: ActorId,
            activatedAt: BaseUtc,
            reasonCode: "BOOTSTRAP_INITIAL_POLICY",
            evidenceReference: "SYSTEM_INIT");

        Assert.Equal(BusinessPolicyActivation.SingletonId, activation.Id);
        Assert.Equal(bundleV1Id, activation.ActiveBusinessPolicyVersionId);
        Assert.False(activation.PublicIntakeEnabled);
        Assert.False(activation.CaptureEnabled);
        Assert.False(activation.ReleaseEnabled);
        Assert.Equal(initialDecisionId, activation.LastDecisionId);

        Assert.Equal(initialDecisionId, initialDecision.Id);
        Assert.Equal(BusinessPolicyActivation.SingletonId, initialDecision.ActivationId);
        Assert.Equal(bundleV1Id, initialDecision.BusinessPolicyVersionId);
        Assert.False(initialDecision.PublicIntakeEnabled);
        Assert.Null(initialDecision.PredecessorDecisionId); // Initial has no predecessor

        // 2. Apply decision transition: updating bundle or flags
        var bundleV2Id = Guid.NewGuid();
        var nextDecisionId = Guid.NewGuid();
        var nextTime = BaseUtc.AddHours(2);
        var adminId = Guid.NewGuid();

        var transitionDecision = activation.ApplyDecision(
            decisionId: nextDecisionId,
            activeBusinessPolicyVersionId: bundleV2Id,
            publicIntakeEnabled: true,
            captureEnabled: true,
            releaseEnabled: false,
            changedBy: adminId,
            changedAt: nextTime,
            reasonCode: "UPGRADE_TO_V2",
            evidenceReference: "ADMIN_APPROVAL_42");

        // Projection updated
        Assert.Equal(bundleV2Id, activation.ActiveBusinessPolicyVersionId);
        Assert.True(activation.PublicIntakeEnabled);
        Assert.True(activation.CaptureEnabled);
        Assert.False(activation.ReleaseEnabled);
        Assert.Equal(nextDecisionId, activation.LastDecisionId);
        Assert.Equal(nextTime, activation.ActivatedAt);
        Assert.Equal(adminId, activation.ActivatedBy);

        // Immutable decision record has linkage to predecessor
        Assert.Equal(nextDecisionId, transitionDecision.Id);
        Assert.Equal(initialDecisionId, transitionDecision.PredecessorDecisionId);
        Assert.Equal(bundleV2Id, transitionDecision.BusinessPolicyVersionId);
        Assert.True(transitionDecision.PublicIntakeEnabled);
        Assert.Equal("UPGRADE_TO_V2", transitionDecision.ReasonCode);

        // Transition timestamp cannot be earlier than or equal to current ActivatedAt
        Assert.Throws<ArgumentException>(() =>
            activation.ApplyDecision(
                decisionId: Guid.NewGuid(),
                activeBusinessPolicyVersionId: bundleV2Id,
                publicIntakeEnabled: true,
                captureEnabled: true,
                releaseEnabled: true,
                changedBy: adminId,
                changedAt: nextTime, // Same time, not strictly later
                reasonCode: "SAME_TIME"));
    }
}
