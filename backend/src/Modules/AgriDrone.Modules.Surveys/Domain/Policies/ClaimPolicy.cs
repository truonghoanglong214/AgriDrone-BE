using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class ClaimPolicy : Entity
{
    private ClaimPolicy() { }

    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public int CalendarWindowDays { get; private set; }
    public string TimeZoneId { get; private set; } = null!;
    public bool ExcludeNoticeDate { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static ClaimPolicy Create(
        Guid id,
        int versionNumber,
        Guid? predecessorVersionId,
        int calendarWindowDays,
        string timeZoneId,
        bool excludeNoticeDate,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);

        if (calendarWindowDays < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calendarWindowDays),
                calendarWindowDays,
                "Calendar window days must be at least 1.");
        }

        var normalizedTimeZone = PolicyValidationGuard.NormalizeRequired(timeZoneId, 100, nameof(timeZoneId));
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"TimeZone '{normalizedTimeZone}' is not recognized.",
                nameof(timeZoneId),
                ex);
        }

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        return new ClaimPolicy
        {
            Id = id,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            CalendarWindowDays = calendarWindowDays,
            TimeZoneId = normalizedTimeZone,
            ExcludeNoticeDate = excludeNoticeDate,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReasonCode = normalizedReasonCode,
            EvidenceReference = normalizedEvidence
        };
    }

    /// <summary>
    /// Computes the exclusive UTC deadline for filing a claim according to BR-CLM-02 and BR-CLM-03.
    /// Notice issued on calendar date D in the configured timezone.
    /// If ExcludeNoticeDate is true, claims are accepted through the end of D + CalendarWindowDays,
    /// so the exclusive deadline is 00:00:00 local time on day D + CalendarWindowDays + 1 converted to UTC.
    /// </summary>
    public DateTimeOffset CalculateExclusiveDeadline(DateTimeOffset noticeCreatedAtUtc)
    {
        DomainGuard.Utc(noticeCreatedAtUtc, nameof(noticeCreatedAtUtc));

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var localDateTime = TimeZoneInfo.ConvertTime(noticeCreatedAtUtc, timeZone);
        var noticeDate = DateOnly.FromDateTime(localDateTime.DateTime);

        var deadlineDate = noticeDate.AddDays(ExcludeNoticeDate ? CalendarWindowDays + 1 : CalendarWindowDays);
        var localMidnight = deadlineDate.ToDateTime(TimeOnly.MinValue);
        var offset = timeZone.GetUtcOffset(localMidnight);

        return new DateTimeOffset(localMidnight, offset).ToUniversalTime();
    }

    public bool IsClaimEligible(DateTimeOffset noticeCreatedAtUtc, DateTimeOffset receivedAtUtc)
    {
        DomainGuard.Utc(receivedAtUtc, nameof(receivedAtUtc));
        if (receivedAtUtc < noticeCreatedAtUtc)
        {
            return false;
        }

        var deadlineExclusive = CalculateExclusiveDeadline(noticeCreatedAtUtc);
        return receivedAtUtc < deadlineExclusive;
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt, nameof(evaluatedAt));
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo, nameof(effectiveTo));
        if (EffectiveTo.HasValue)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.WindowAlreadyClosed,
                "A closed claim policy effective window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.InvalidEffectiveWindow,
                "Claim policy effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
