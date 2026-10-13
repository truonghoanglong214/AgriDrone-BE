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
                SurveyAppointmentStatus.Superseded,
                SurveyAppointmentStatus.Cancelled
            }
        };

    private SurveyAppointment() { }

    public Guid SurveyOrderId { get; private set; }
    public SurveyAppointmentPurpose Purpose { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid? PreviousAppointmentId { get; private set; }
    public Guid? ProposedBySystemManagerId { get; private set; }
    public DateTimeOffset ProposedStartAt { get; private set; }
    public DateTimeOffset ProposedEndAt { get; private set; }
    public SurveyAppointmentStatus Status { get; private set; }
    public Guid? ConfirmedByTenantOwnerId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? RescheduleReason { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static SurveyAppointment Create(
        Guid surveyOrderId,
        SurveyAppointmentPurpose purpose,
        int revisionNumber,
        Guid? previousAppointmentId,
        Guid proposedBySystemManagerId,
        DateTimeOffset proposedStartAt,
        DateTimeOffset proposedEndAt,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(proposedBySystemManagerId);
        DomainGuard.Utc(createdAt);
        EnsureWindow(proposedStartAt, proposedEndAt);

        if (revisionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revisionNumber),
                "Appointment revision number must be positive.");
        }

        if ((revisionNumber == 1 && previousAppointmentId.HasValue) ||
            (revisionNumber > 1 && !previousAppointmentId.HasValue))
        {
            throw new ArgumentException(
                "Only appointment revisions after the first require a previous appointment.",
                nameof(previousAppointmentId));
        }

        if (previousAppointmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Previous appointment identifier cannot be empty.",
                nameof(previousAppointmentId));
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        return new SurveyAppointment
        {
            Id = Guid.NewGuid(),
            SurveyOrderId = surveyOrderId,
            Purpose = purpose,
            RevisionNumber = revisionNumber,
            PreviousAppointmentId = previousAppointmentId,
            ProposedBySystemManagerId = proposedBySystemManagerId,
            ProposedStartAt = proposedStartAt,
            ProposedEndAt = proposedEndAt,
            Status = SurveyAppointmentStatus.Proposed,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

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
        DomainGuard.Utc(confirmedAt);
        if (confirmedAt >= ProposedEndAt)
        {
            throw new SurveyDomainException(
                AppointmentDomainErrorCodes.ConfirmationWindowExpired,
                "The appointment can no longer be confirmed after its proposed window ended.");
        }

        TransitionTo(SurveyAppointmentStatus.Confirmed, confirmedAt);
        ConfirmedByTenantOwnerId = tenantOwnerId;
        ConfirmedAt = confirmedAt;
        RescheduleReason = null;
    }

    public void RequestReschedule(string reason, DateTimeOffset requestedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                "Reschedule reason cannot exceed 1000 characters.");
        }

        TransitionTo(SurveyAppointmentStatus.RescheduleRequested, requestedAt);
        ConfirmedByTenantOwnerId = null;
        ConfirmedAt = null;
        RescheduleReason = reason.Trim();
    }

    public void Supersede(DateTimeOffset supersededAt)
    {
        TransitionTo(SurveyAppointmentStatus.Superseded, supersededAt);
        SupersededAt = supersededAt;
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
