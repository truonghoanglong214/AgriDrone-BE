using System.Globalization;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

/// <summary>
/// Real BVA tests for:
/// - TC-F01-U02: Price / service / deposit effective boundary (just before vs at effectiveFrom) and order snapshot capture.
/// - TC-F02-U02: Appointment confirmation expiry boundary (just before vs at ProposedEndAt) and date window resolution.
/// </summary>
public sealed class PricingSnapshotBvaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CutoverUtc = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();

    // ─── TC-F01-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F01-U02")]
    public void CommercialPoliciesAndSnapshotCaptureExactVersionAcrossEffectiveBoundary()
    {
        var serviceId = Guid.NewGuid();
        var rev1Id = Guid.NewGuid();
        var rev2Id = Guid.NewGuid();
        var price1Id = Guid.NewGuid();
        var price2Id = Guid.NewGuid();
        var dep1Id = Guid.NewGuid();
        var dep2Id = Guid.NewGuid();

        var rev1 = SurveyServiceRevision.Create(
            rev1Id, serviceId, 1, null, "MAP v1", "Initial", true, false,
            T0, CutoverUtc, ActorId, T0, "INIT");
        var rev2 = SurveyServiceRevision.Create(
            rev2Id, serviceId, 2, rev1Id, "MAP v2", "Updated", true, false,
            CutoverUtc, null, ActorId, T0, "UPDATE");

        var price1 = SurveyServicePrice.CreatePerPoleV3(
            price1Id, serviceId, 1, null, 1_000m, CurrencyCode.Vnd,
            T0, CutoverUtc, ActorId, T0, "PRICE_V1");
        var price2 = SurveyServicePrice.CreatePerPoleV3(
            price2Id, serviceId, 2, price1Id, 2_000m, CurrencyCode.Vnd,
            CutoverUtc, null, ActorId, T0, "PRICE_V2");

        var dep1 = SurveyDepositPolicy.Create(
            dep1Id, 1, null, 100_000m, CurrencyCode.Vnd,
            T0, CutoverUtc, ActorId, T0, "DEP_V1");
        var dep2 = SurveyDepositPolicy.Create(
            dep2Id, 2, dep1Id, 150_000m, CurrencyCode.Vnd,
            CutoverUtc, null, ActorId, T0, "DEP_V2");

        // Just before cutover (1 tick prior) -> v1 active, v2 inactive
        var beforeCutover = CutoverUtc.AddTicks(-1);
        Assert.True(rev1.IsEffectiveAt(beforeCutover));
        Assert.False(rev2.IsEffectiveAt(beforeCutover));
        Assert.True(price1.IsEffectiveAt(beforeCutover));
        Assert.False(price2.IsEffectiveAt(beforeCutover));
        Assert.True(dep1.IsEffectiveAt(beforeCutover));
        Assert.False(dep2.IsEffectiveAt(beforeCutover));

        var snapshotBefore = SurveyOrderCommercialSnapshot.Create(
            Guid.NewGuid(),
            BuildResolved(rev1, price1, dep1, beforeCutover),
            beforeCutover);
        Assert.Equal(1, snapshotBefore.PriceVersionNumber);
        Assert.Equal(1_000m, snapshotBefore.PricePerPole);
        Assert.Equal(100_000m, snapshotBefore.DepositAmount);

        // Exactly at cutover -> v1 closed, v2 active
        Assert.False(rev1.IsEffectiveAt(CutoverUtc));
        Assert.True(rev2.IsEffectiveAt(CutoverUtc));
        Assert.False(price1.IsEffectiveAt(CutoverUtc));
        Assert.True(price2.IsEffectiveAt(CutoverUtc));
        Assert.False(dep1.IsEffectiveAt(CutoverUtc));
        Assert.True(dep2.IsEffectiveAt(CutoverUtc));

        var snapshotAt = SurveyOrderCommercialSnapshot.Create(
            Guid.NewGuid(),
            BuildResolved(rev2, price2, dep2, CutoverUtc),
            CutoverUtc);
        Assert.Equal(2, snapshotAt.PriceVersionNumber);
        Assert.Equal(2_000m, snapshotAt.PricePerPole);
        Assert.Equal(150_000m, snapshotAt.DepositAmount);
    }

    // ─── TC-F02-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F02-U02")]
    public void ConfirmingAppointmentJustBeforeAndAtExpiryEnforcesBoundary()
    {
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 11, 20, 1, 0, 0, TimeSpan.Zero);
        var windowStart = createdAt.AddHours(1);
        var windowExpiry = createdAt.AddHours(3);

        // 1 tick before expiry -> confirmed
        var validAppt = SurveyAppointment.Create(
            orderId, SurveyAppointmentPurpose.Capture, 1, null, managerId,
            windowStart, windowExpiry, createdAt);
        validAppt.Confirm(ownerId, windowExpiry.AddTicks(-1));
        Assert.Equal(SurveyAppointmentStatus.Confirmed, validAppt.Status);
        Assert.Equal(ownerId, validAppt.ConfirmedByTenantOwnerId);

        // Exactly at expiry -> rejected and remains Proposed
        var expiredAppt = SurveyAppointment.Create(
            orderId, SurveyAppointmentPurpose.Capture, 1, null, managerId,
            windowStart, windowExpiry, createdAt);
        var ex = Assert.Throws<SurveyDomainException>(() =>
            expiredAppt.Confirm(ownerId, windowExpiry));
        Assert.Equal(AppointmentDomainErrorCodes.ConfirmationWindowExpired, ex.Code);
        Assert.Equal(SurveyAppointmentStatus.Proposed, expiredAppt.Status);
        Assert.Null(expiredAppt.ConfirmedAt);
    }

    [Fact, Trait("TC", "TC-F02-U02")]
    public void DateWindowRejectsExpiredDateAndAcceptsCurrentDateInVnTimezone()
    {
        var nowUtc = new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero); // 10:00 +07
        var vnTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, vnTz).DateTime);

        var expiredResult = SurveyAppointmentWindow.TryResolveForDate(
            localToday.AddDays(-1), nowUtc, out _, out _, out var error);
        Assert.False(expiredResult);
        Assert.False(string.IsNullOrWhiteSpace(error));

        var todayResult = SurveyAppointmentWindow.TryResolveForDate(
            localToday, nowUtc, out var startUtc, out var endUtc, out var todayError);
        Assert.True(todayResult);
        Assert.Null(todayError);
        Assert.Equal(nowUtc.AddMinutes(5), startUtc);
        Assert.True(endUtc > startUtc);
    }

    private static ResolvedSurveyCommercialSnapshot BuildResolved(
        SurveyServiceRevision rev,
        SurveyServicePrice price,
        SurveyDepositPolicy deposit,
        DateTimeOffset evaluatedAt) =>
        new(
            BusinessPolicyVersionId: Guid.NewGuid(),
            PolicyFingerprint: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SurveyServiceRevisionId: rev.Id,
            SurveyServiceRevisionVersion: rev.VersionNumber,
            ServiceCode: "MAP",
            MapsPoles: rev.MapsPoles,
            ScreensDisease: rev.ScreensDisease,
            PriceVersionId: price.Id,
            PriceVersionNumber: price.VersionNumber ?? 1,
            PricePerPole: price.PricePerPole!.Value.Amount,
            Currency: price.Currency,
            DepositPolicyId: deposit.Id,
            DepositVersionNumber: deposit.VersionNumber,
            DepositAmount: deposit.Amount,
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
            EvaluatedAt: evaluatedAt);
}
