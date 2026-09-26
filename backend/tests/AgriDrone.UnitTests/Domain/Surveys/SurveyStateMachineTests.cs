using System.Reflection;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyStateMachineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SurveyRequestStatus.Submitted, SurveyRequestStatus.UnderReview, true)]
    [InlineData(SurveyRequestStatus.Submitted, SurveyRequestStatus.Approved, false)]
    [InlineData(SurveyRequestStatus.UnderReview, SurveyRequestStatus.Approved, true)]
    [InlineData(SurveyRequestStatus.UnderReview, SurveyRequestStatus.Rejected, true)]
    [InlineData(SurveyRequestStatus.Approved, SurveyRequestStatus.Withdrawn, false)]
    public void RequestTransitionTableIsExplicit(
        SurveyRequestStatus from,
        SurveyRequestStatus to,
        bool expected) =>
        Assert.Equal(expected, SurveyRequest.CanTransition(from, to));

    [Theory]
    [InlineData(SurveyOrderStatus.PendingScopeConfirmation, SurveyOrderStatus.AwaitingAppointment, true)]
    [InlineData(SurveyOrderStatus.AwaitingAppointment, SurveyOrderStatus.AwaitingPayment, true)]
    [InlineData(SurveyOrderStatus.AwaitingPayment, SurveyOrderStatus.ReadyForOperations, true)]
    [InlineData(SurveyOrderStatus.ReadyForOperations, SurveyOrderStatus.InProgress, true)]
    [InlineData(SurveyOrderStatus.InProgress, SurveyOrderStatus.PendingReview, true)]
    [InlineData(SurveyOrderStatus.PendingReview, SurveyOrderStatus.Completed, true)]
    [InlineData(SurveyOrderStatus.InProgress, SurveyOrderStatus.Cancelled, false)]
    [InlineData(SurveyOrderStatus.Completed, SurveyOrderStatus.Cancelled, false)]
    public void OrderTransitionTableIsExplicit(
        SurveyOrderStatus from,
        SurveyOrderStatus to,
        bool expected) =>
        Assert.Equal(expected, SurveyOrder.CanTransition(from, to));

    [Fact]
    public void SupportingTransitionTablesRejectBypasses()
    {
        Assert.True(SurveyAppointment.CanTransition(
            SurveyAppointmentStatus.Proposed,
            SurveyAppointmentStatus.Confirmed));
        Assert.False(SurveyAppointment.CanTransition(
            SurveyAppointmentStatus.Cancelled,
            SurveyAppointmentStatus.Proposed));
        Assert.True(SurveyPayment.CanTransition(
            SurveyPaymentStatus.Processing,
            SurveyPaymentStatus.Confirmed));
        Assert.False(SurveyPayment.CanTransition(
            SurveyPaymentStatus.Failed,
            SurveyPaymentStatus.Confirmed));
        Assert.True(PriceAdjustment.CanTransition(
            PriceAdjustmentStatus.Pending,
            PriceAdjustmentStatus.Approved));
        Assert.False(PriceAdjustment.CanTransition(
            PriceAdjustmentStatus.Pending,
            PriceAdjustmentStatus.Applied));
        Assert.True(SurveyResult.CanTransition(
            SurveyResultStatus.Approved,
            SurveyResultStatus.Published));
        Assert.False(SurveyResult.CanTransition(
            SurveyResultStatus.PendingReview,
            SurveyResultStatus.Published));
    }

    [Fact]
    public void RequestDecisionRequiresReviewAndKeepsAppendOnlySnapshot()
    {
        var request = Create<SurveyRequest>(
            (nameof(SurveyRequest.Status), SurveyRequestStatus.Submitted),
            (nameof(SurveyRequest.UpdatedAt), Now),
            (nameof(SurveyRequest.Version), 4u));

        request.StartReview(Now.AddMinutes(1));
        using var checklist = JsonDocument.Parse("{\"managerEligible\":true}");
        request.Approve(checklist, "Eligible", Guid.NewGuid(), Now.AddMinutes(2));

        Assert.Equal(SurveyRequestStatus.Approved, request.Status);
        Assert.Equal(6u, request.Version);
        var review = Assert.Single(request.Reviews);
        Assert.Equal(SurveyReviewDecision.Approved, review.Decision);
        Assert.True(review.ChecklistSnapshot.RootElement.GetProperty("managerEligible").GetBoolean());
    }

    [Fact]
    public void RequestCannotBypassReview()
    {
        var request = Create<SurveyRequest>(
            (nameof(SurveyRequest.Status), SurveyRequestStatus.Submitted),
            (nameof(SurveyRequest.UpdatedAt), Now));
        using var checklist = JsonDocument.Parse("{}");

        var exception = Assert.Throws<SurveyDomainException>(() =>
            request.Approve(checklist, "Eligible", Guid.NewGuid(), Now.AddMinutes(1)));

        Assert.Equal(SurveyRequestDomainErrorCodes.InvalidTransition, exception.Code);
        Assert.Empty(request.Reviews);
    }

    [Fact]
    public void OrderUsesNamedLinearTransitionsAndRejectsLateCancellation()
    {
        var order = Create<SurveyOrder>(
            (nameof(SurveyOrder.Status), SurveyOrderStatus.PendingScopeConfirmation),
            (nameof(SurveyOrder.UpdatedAt), Now));

        order.MarkAwaitingAppointment(Now.AddMinutes(1));
        order.MarkAwaitingPayment(Now.AddMinutes(2));
        order.MarkReadyForOperations(Now.AddMinutes(3));
        order.StartOperations(Now.AddMinutes(4));

        var exception = Assert.Throws<SurveyDomainException>(() =>
            order.Cancel(Now.AddMinutes(5)));

        Assert.Equal(SurveyOrderDomainErrorCodes.InvalidTransition, exception.Code);
        Assert.Equal(SurveyOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void AppointmentRescheduleClearsConfirmation()
    {
        var ownerId = Guid.NewGuid();
        var appointment = Create<SurveyAppointment>(
            (nameof(SurveyAppointment.Status), SurveyAppointmentStatus.Proposed),
            (nameof(SurveyAppointment.UpdatedAt), Now));

        appointment.Confirm(ownerId, Now.AddMinutes(1));
        appointment.RequestReschedule("Weather", Now.AddMinutes(2));

        Assert.Equal(SurveyAppointmentStatus.RescheduleRequested, appointment.Status);
        Assert.Null(appointment.ConfirmedByTenantOwnerId);
        Assert.Null(appointment.ConfirmedAt);
        Assert.Equal("Weather", appointment.RescheduleReason);
    }

    [Fact]
    public void PaymentConfirmationRequiresProviderEvidence()
    {
        var payment = Create<SurveyPayment>(
            (nameof(SurveyPayment.Status), SurveyPaymentStatus.Pending),
            (nameof(SurveyPayment.UpdatedAt), Now));

        Assert.Throws<ArgumentException>(() => payment.Confirm(" ", Now.AddMinutes(1)));
        payment.StartProcessing(Now.AddMinutes(1));
        payment.Confirm(" provider-01 ", Now.AddMinutes(2));

        Assert.Equal(SurveyPaymentStatus.Confirmed, payment.Status);
        Assert.Equal("provider-01", payment.ProviderReference);
        Assert.Equal(Now.AddMinutes(2), payment.ConfirmedAt);
    }

    [Fact]
    public void ResultMustBeHumanApprovedBeforePublication()
    {
        using var provenance = JsonDocument.Parse("{\"model\":\"v2\"}");
        var result = SurveyResult.CreateForManagerReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SurveyServiceType.PlantHealth,
            provenance,
            Now);

        var exception = Assert.Throws<SurveyDomainException>(() =>
            result.Publish(Guid.NewGuid(), Now.AddMinutes(1)));
        Assert.Equal(SurveyResultDomainErrorCodes.InvalidTransition, exception.Code);

        result.Approve(Guid.NewGuid(), Now.AddMinutes(1));
        result.Publish(Guid.NewGuid(), Now.AddMinutes(2));
        Assert.Equal(SurveyResultStatus.Published, result.Status);
        Assert.Equal(2u, result.Version);
    }

    [Fact]
    public void PriceAdjustmentMustBeApprovedBeforeItIsApplied()
    {
        var adjustment = Create<PriceAdjustment>(
            (nameof(PriceAdjustment.Status), PriceAdjustmentStatus.Pending));

        var exception = Assert.Throws<SurveyDomainException>(() =>
            adjustment.MarkApplied(Now));
        Assert.Equal(
            PriceAdjustmentDomainErrorCodes.InvalidTransition,
            exception.Code);

        adjustment.Approve(Guid.NewGuid(), Now);
        adjustment.MarkApplied(Now.AddMinutes(1));
        Assert.Equal(PriceAdjustmentStatus.Applied, adjustment.Status);
    }

    [Fact]
    public void ExpectedVersionMismatchHasStableConflictCode()
    {
        var order = Create<SurveyOrder>((nameof(SurveyOrder.Version), 7u));

        var exception = Assert.Throws<SurveyDomainException>(() =>
            order.EnsureVersion(6));

        Assert.Equal(SurveyOrderDomainErrorCodes.VersionConflict, exception.Code);
    }

    [Fact]
    public void DomainRejectsNonUtcAndBackdatedTransitions()
    {
        var order = Create<SurveyOrder>(
            (nameof(SurveyOrder.Status), SurveyOrderStatus.PendingScopeConfirmation),
            (nameof(SurveyOrder.UpdatedAt), Now));

        Assert.Throws<ArgumentException>(() =>
            order.MarkAwaitingAppointment(Now.ToOffset(TimeSpan.FromHours(7))));
        Assert.Throws<ArgumentException>(() =>
            order.MarkAwaitingAppointment(Now.AddTicks(-1)));
    }

    private static T Create<T>(params (string Property, object Value)[] values)
        where T : class
    {
        var instance = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        foreach (var (property, value) in values)
        {
            typeof(T).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(instance, value);
        }

        return instance;
    }
}
