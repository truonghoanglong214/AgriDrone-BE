using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyAppointment : Entity
{
    private static readonly Dictionary<SurveyAppointmentStatus, IReadOnlySet<SurveyAppointmentStatus>>
        AllowedTransitions = new Dictionary<SurveyAppointmentStatus, IReadOnlySet<SurveyAppointmentStatus>>
        {
            [SurveyAppointmentStatus.Proposed] = new HashSet<SurveyAppointmentStatus>
            {
                SurveyAppointmentStatus.Confirmed,
                SurveyAppointmentStatus.RescheduleRequested,
                SurveyAppointmentStatus.Cancelled
            },
            [SurveyAppointmentStatus.Confirmed] = new HashSet<SurveyAppointmentStatus>
            {
                SurveyAppointmentStatus.RescheduleRequested,
                SurveyAppointmentStatus.Cancelled
            },
            [SurveyAppointmentStatus.RescheduleRequested] = new HashSet<SurveyAppointmentStatus>
            {
                SurveyAppointmentStatus.Proposed,
                SurveyAppointmentStatus.Cancelled
            }
        };

    private SurveyAppointment() { }

    public Guid SurveyOrderId { get; private set; }
    public DateTimeOffset ProposedStartAt { get; private set; }
    public DateTimeOffset ProposedEndAt { get; private set; }
    public SurveyAppointmentStatus Status { get; private set; }
    public Guid? ConfirmedByTenantOwnerId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? RescheduleReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static bool CanTransition(
        SurveyAppointmentStatus from,
        SurveyAppointmentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                AppointmentDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void Confirm(Guid tenantOwnerId, DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(tenantOwnerId);
        TransitionTo(SurveyAppointmentStatus.Confirmed, confirmedAt);
        ConfirmedByTenantOwnerId = tenantOwnerId;
        ConfirmedAt = confirmedAt;
        RescheduleReason = null;
    }

    public void RequestReschedule(string reason, DateTimeOffset requestedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        TransitionTo(SurveyAppointmentStatus.RescheduleRequested, requestedAt);
        ConfirmedByTenantOwnerId = null;
        ConfirmedAt = null;
        RescheduleReason = reason.Trim();
    }

    public void Repropose(
        DateTimeOffset proposedStartAt,
        DateTimeOffset proposedEndAt,
        DateTimeOffset proposedAt)
    {
        EnsureWindow(proposedStartAt, proposedEndAt);
        TransitionTo(SurveyAppointmentStatus.Proposed, proposedAt);
        ProposedStartAt = proposedStartAt;
        ProposedEndAt = proposedEndAt;
        RescheduleReason = null;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        TransitionTo(SurveyAppointmentStatus.Cancelled, cancelledAt);
        ConfirmedByTenantOwnerId = null;
        ConfirmedAt = null;
    }

    private void TransitionTo(SurveyAppointmentStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            AppointmentDomainErrorCodes.InvalidTransition);
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }

    private static void EnsureWindow(
        DateTimeOffset proposedStartAt,
        DateTimeOffset proposedEndAt)
    {
        DomainGuard.Utc(proposedStartAt);
        DomainGuard.Utc(proposedEndAt);
        if (proposedEndAt <= proposedStartAt)
        {
            throw new ArgumentException("Appointment end must be after its start.");
        }
    }
}
