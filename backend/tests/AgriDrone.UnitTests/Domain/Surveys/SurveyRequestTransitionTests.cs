using System.Reflection;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

/// <summary>
/// Unit tests covering:
///   TC-ST-NEG-01  – Cannot approve a request that is still Submitted
///   TC-ST-REQ-01  – Can approve from UnderReview
///   TC-ST-REQ-02  – Can reject from UnderReview
///   TC-ST-APT-01  – Owner can confirm a Proposed appointment
///   TC-ST-APT-02  – Owner can request a reschedule from Confirmed
/// </summary>
public sealed class SurveyRequestTransitionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ //
    // Helper: create a bare entity by setting properties via reflection,
    // matching the pattern used in SurveyStateMachineTests.
    // ------------------------------------------------------------------ //

    private static T Create<T>(params (string Property, object Value)[] values)
        where T : class
    {
        var instance = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        foreach (var (property, value) in values)
        {
            typeof(T)
                .GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(instance, value);
        }

        return instance;
    }

    // ------------------------------------------------------------------ //
    // TC-ST-NEG-01 – Submitted → Approved is NOT allowed
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-NEG-01")]
    public void CannotApproveRequestFromSubmitted()
    {
        var canApprove = SurveyRequest.CanTransition(
            SurveyRequestStatus.Submitted,
            SurveyRequestStatus.Approved);

        Assert.False(canApprove);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-REQ-01 – UnderReview → Approved IS allowed
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-REQ-01")]
    public void CanApproveRequestFromUnderReview()
    {
        var canApprove = SurveyRequest.CanTransition(
            SurveyRequestStatus.UnderReview,
            SurveyRequestStatus.Approved);

        Assert.True(canApprove);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-REQ-02 – UnderReview → Rejected IS allowed
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-REQ-02")]
    public void CanRejectRequestFromUnderReview()
    {
        var canReject = SurveyRequest.CanTransition(
            SurveyRequestStatus.UnderReview,
            SurveyRequestStatus.Rejected);

        Assert.True(canReject);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-APT-01 – Owner confirms a Proposed appointment → Confirmed
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-APT-01")]
    public void OwnerCanConfirmProposedAppointment()
    {
        // Arrange: build a Proposed appointment whose window has not yet ended
        var windowStart = Now.AddHours(1);
        var windowEnd   = Now.AddHours(3);
        var ownerId     = Guid.NewGuid();

        var appointment = Create<SurveyAppointment>(
            (nameof(SurveyAppointment.Status),          SurveyAppointmentStatus.Proposed),
            (nameof(SurveyAppointment.UpdatedAt),       Now),
            (nameof(SurveyAppointment.ProposedStartAt), windowStart),
            (nameof(SurveyAppointment.ProposedEndAt),   windowEnd));

        // Act: owner confirms before the window closes
        var confirmedAt = Now.AddMinutes(30);
        appointment.Confirm(ownerId, confirmedAt);

        // Assert
        Assert.Equal(SurveyAppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(ownerId, appointment.ConfirmedByTenantOwnerId);
        Assert.Equal(confirmedAt, appointment.ConfirmedAt);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-APT-02 – From Confirmed, owner requests reschedule → RescheduleRequested
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-APT-02")]
    public void OwnerCanRequestRescheduleFromConfirmed()
    {
        // Arrange: build a Confirmed appointment
        var windowStart = Now.AddHours(1);
        var windowEnd   = Now.AddHours(3);
        var ownerId     = Guid.NewGuid();
        var confirmedAt = Now.AddMinutes(10);

        var appointment = Create<SurveyAppointment>(
            (nameof(SurveyAppointment.Status),          SurveyAppointmentStatus.Proposed),
            (nameof(SurveyAppointment.UpdatedAt),       Now),
            (nameof(SurveyAppointment.ProposedStartAt), windowStart),
            (nameof(SurveyAppointment.ProposedEndAt),   windowEnd));

        appointment.Confirm(ownerId, confirmedAt);
        Assert.Equal(SurveyAppointmentStatus.Confirmed, appointment.Status);

        // Act: owner requests a reschedule
        var rescheduleAt = confirmedAt.AddMinutes(1);
        appointment.RequestReschedule("Bad weather forecast", rescheduleAt);

        // Assert
        Assert.Equal(SurveyAppointmentStatus.RescheduleRequested, appointment.Status);
        Assert.Equal("Bad weather forecast", appointment.RescheduleReason);
        Assert.Null(appointment.ConfirmedByTenantOwnerId);
        Assert.Null(appointment.ConfirmedAt);
    }
}
